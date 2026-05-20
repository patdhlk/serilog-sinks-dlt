# Docker dev container

An Ubuntu 24.04 (Noble) image with the .NET 9 SDK and `dlt-daemon` **built
from source** so it speaks the Unix-socket IPC our sink targets. The daemon
is started automatically by the container's entrypoint, so the `RealDaemon`
integration tests and the sample app can run end-to-end against a live
`dlt-daemon` without installing anything on the host.

## Why we build dlt-daemon from source

Ubuntu's `apt` package for `dlt-daemon` is compiled with **FIFO IPC**, not
Unix-domain-socket IPC. `/tmp/dlt` in the apt build is a named pipe
(`prw--w----`), not a socket — our `UnixSocketTransport` can't talk to it.

This Dockerfile clones COVESA `dlt-daemon` at v3.0.0 and builds it with
`cmake -DDLT_IPC=UNIX_SOCKET`, which makes `/tmp/dlt` a real `AF_UNIX`
socket. Build adds ~30 s to the first `docker build`; cached layers make
subsequent rebuilds fast.

## How daemon ingestion works end-to-end

1. Container start → `entrypoint.sh` writes `LoggingMode = 3` (BOTH) to
   `/tmp/dlt-runtime.cfg`. This is critical: the daemon's user-mode
   defaults to `EXTERNAL` (forward-only) and offline-trace writing is
   gated on `INTERNAL` or `BOTH`. Without this, the daemon receives
   our messages but discards them silently.
2. Daemon starts in the background, listening on `/tmp/dlt` (AF_UNIX) +
   TCP 3490.
3. Our sink connects → sends `DltUserHeader` + `REGISTER_APPLICATION`
   greeting → daemon registers app.
4. Each unique CTID triggers a `REGISTER_CONTEXT` from the dispatcher
   before the first LOG with that context — matches libdlt's behavior.
5. LOG messages are sent with libdlt-compatible HTYP (`UEH | WEID | WSID |
   WTMS | VERS=1`) and `DltUserHeader(LOG)` prefix.
6. A background reader-loop in the transport drains daemon-sent control
   messages (`LOG_STATE`, `LOG_LEVEL`) so the daemon's send-side never
   stalls. Bytes are discarded — we don't currently apply log-level
   updates to a filter.

Result: messages land in the daemon's offline trace at `/var/log/dlt/`
and `dlt-convert -a` decodes them correctly. The `demo.sh` script
demonstrates this end-to-end.

## Build the image

From the **repo root** (the build context needs to see `docker/*` and copy
those files in):

```bash
docker build -f docker/Dockerfile -t serilog-sinks-dlt-dev .
```

Pin a specific daemon version if you want:

```bash
docker build -f docker/Dockerfile --build-arg DLT_DAEMON_VERSION=v2.18.10 \
    -t serilog-sinks-dlt-dev .
```

## Interactive dev shell

Mount the repo into `/workspace` and drop into bash:

```bash
docker run --rm -it -v "$PWD:/workspace" serilog-sinks-dlt-dev
```

Inside the container:

```bash
# Unit tests (fake daemon, no real daemon needed)
dotnet test --filter "Category!=RealDaemon"

# Integration tests against the running dlt-daemon
dotnet test tests/Serilog.Sinks.Dlt.IntegrationTests --filter "Category=RealDaemon"

# Run the sample app — file sink + Unix-socket daemon sink (DLT_DAEMON_AVAILABLE=1 is preset)
dotnet run --project samples/Demo -- /tmp/demo.dlt

# Inspect what the daemon recorded in offline trace
dlt-convert -a /var/log/dlt/*.dlt
```

## One-shot smoke test

Runs build → sample → integration test → daemon trace dump, then exits:

```bash
docker run --rm -v "$PWD:/workspace" serilog-sinks-dlt-dev demo.sh
```

## How it's wired

- `Dockerfile` — single image, `mcr.microsoft.com/dotnet/sdk:9.0-noble` base;
  builds `dlt-daemon` from COVESA source with Unix-socket IPC.
- `entrypoint.sh` — `dlt-daemon -d -c /etc/dlt.conf` in the background, polls
  for `/tmp/dlt` (the AF_UNIX socket), then `exec "$@"`. The daemon dies
  with the container.
- `dlt.conf` — offline trace files to `/var/log/dlt`, 10 MB per file, 50 MB
  cap; control socket at `/tmp/dlt-ctrl.sock`.
- `demo.sh` — convenience script for the non-interactive smoke test.

Environment variables baked in so `RealDaemonTests` and `samples/Demo`
Just Work:

| Var | Value |
|---|---|
| `DLT_DAEMON_AVAILABLE` | `1` |
| `DLT_SOCKET_PATH`      | `/tmp/dlt` |
| `DLT_TRACE_DIR`        | `/var/log/dlt` |

## Notes

- The `obj/` and `bin/` directories are shared with the host via the bind
  mount. On Linux/macOS that's fine; if you also build on the host, expect
  the first build inside the container to rebuild from scratch.
- The container has no persistent NuGet cache — first restore is slow.
  Mount `~/.nuget/packages` if you want a warm cache:
  ```bash
  docker run --rm -it \
      -v "$PWD:/workspace" \
      -v "$HOME/.nuget/packages:/root/.nuget/packages" \
      serilog-sinks-dlt-dev
  ```
- Daemon exits when the container exits; nothing to clean up on the host.
