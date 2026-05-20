# Serilog.Sinks.Dlt

[![NuGet Version](https://img.shields.io/nuget/v/Serilog.Sinks.Dlt.svg?style=flat-square)](https://www.nuget.org/packages/Serilog.Sinks.Dlt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Serilog.Sinks.Dlt.svg?style=flat-square)](https://www.nuget.org/packages/Serilog.Sinks.Dlt)
[![Build](https://img.shields.io/github/actions/workflow/status/patdhlk/serilog-sinks-dlt/ci.yml?branch=main&style=flat-square)](https://github.com/patdhlk/serilog-sinks-dlt/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)

A [Serilog](https://serilog.net) sink that emits log events as **COVESA DLT v1** (Diagnostic Log and Trace) messages — to a running `dlt-daemon` (Unix domain socket or TCP), or to a rotating `.dlt` file readable by [DLT Viewer](https://github.com/COVESA/dlt-viewer).

Verbose-mode encoding: each Serilog structured property arrives in DLT Viewer as a typed argument (string / int / float / bool / raw bytes), so filters and value-based search work on the original data — not on a rendered string.

## Install

```sh
dotnet add package Serilog.Sinks.Dlt
```

Targets `net8.0` and `net9.0`.

## Quick start

```csharp
using Serilog;

using var log = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Dlt(appId: "MYAP", socketPath: "/tmp/dlt")     // daemon (Unix socket)
    .WriteTo.DltFile("logs/app.dlt", appId: "MYAP")          // rotating .dlt file
    .CreateLogger();

log.Information("order {orderId} processed in {ms} ms", 42, 17.3);
```

Three sink methods:

| Method | Destination |
|---|---|
| `.WriteTo.Dlt(appId, socketPath, ...)` | `dlt-daemon` over Unix domain socket |
| `.WriteTo.DltTcp(appId, host, port, ...)` | `dlt-daemon` over TCP/IP |
| `.WriteTo.DltFile(path, appId, ...)` | rotating `.dlt` file with DLT storage header |

All accept `ecuId`, `defaultContextId`, `queueCapacity`, `shutdownTimeout`, and `contextIdMap`, plus method-specific options (reconnect timings for the daemon paths, size/retention for the file path).

### `appsettings.json` configuration

`Serilog.Settings.Configuration` reads the sink reflectively, so the JSON form mirrors the C# parameters:

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "Dlt",    "Args": { "appId": "MYAP" } },
      { "Name": "DltTcp", "Args": { "appId": "MYAP", "host": "192.168.1.10" } },
      { "Name": "DltFile","Args": { "path": "logs/app.dlt", "appId": "MYAP" } }
    ]
  }
}
```

## How identifiers and levels map

| DLT field | Source |
|---|---|
| `ECU`  | `ecuId` option (default `"ECU1"`) |
| `APID` | `appId` option (required) |
| `CTID` | hashed from Serilog `SourceContext` (FNV-1a → 4-char base32); override via `contextIdMap` or fall back to `defaultContextId` |
| Log level | Verbose→6, Debug→5, Information→4, Warning→3, Error→2, Fatal→1 |

## Behavior under load

The hot path (`Log.Information(...)`) enqueues a small struct onto a bounded `Channel<T>` (default capacity 10 000) and returns immediately. A background worker drains the queue and writes to the transport. On daemon outages a decorator reconnects with exponential backoff (500 ms → 30 s, with jitter). When the queue overflows the **oldest** event is dropped and a rate-limited `Serilog.Debugging.SelfLog` message reports the drop count.

## Trying it without installing anything

The repo ships a Docker dev container that builds `dlt-daemon` from COVESA source with `DLT_IPC=UNIX_SOCKET` and runs the sample end-to-end. See [`docker/README.md`](docker/README.md). The one-liner:

```sh
docker build -f docker/Dockerfile -t serilog-sinks-dlt-dev .
docker run --rm -v "$PWD:/workspace" serilog-sinks-dlt-dev demo.sh
```

After it finishes, `.dlt` files appear under `.demo-output/` on your host, ready to open in DLT Viewer (`File → Open DLT File…`). For live TCP streaming into DLT Viewer, publish port 3490: `-p 3490:3490`, then point the viewer at `127.0.0.1:3490` with ECU ID `ECU1`.

## What's not in this release

- DLT non-verbose mode
- Acting on inbound DLT control messages (the transport reads and discards them; it doesn't apply daemon-pushed log-level changes to a per-context filter)
- TLS for TCP
- Multicast / UDP / serial transports

## Contributing

Bug reports and PRs welcome at [github.com/patdhlk/serilog-sinks-dlt](https://github.com/patdhlk/serilog-sinks-dlt). See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the dev loop.

## License

[MIT](LICENSE) — Copyright (c) 2026 Patrick Dahlke.
