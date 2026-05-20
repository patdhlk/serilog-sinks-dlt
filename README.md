# Serilog.Sinks.Dlt

A Serilog sink that emits log events as COVESA DLT v1 (Diagnostic Log and Trace) messages — to a running `dlt-daemon` (Unix domain socket or TCP), or to a rotating `.dlt` file readable by [DLT Viewer](https://github.com/COVESA/dlt-viewer).

Verbose-mode encoding: each Serilog structured property arrives in DLT Viewer as a typed argument (string / int / float / bool / raw bytes) so filters and value-based search work.

## Install

```
dotnet add package Serilog.Sinks.Dlt
```

Targets `net8.0` and `net9.0`.

## Quick start

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Dlt(appId: "MYAP", socketPath: "/tmp/dlt")
    .WriteTo.DltFile("logs/app.dlt", appId: "MYAP")
    .CreateLogger();

Log.Information("order {orderId} processed in {ms} ms", 42, 17.3);
```

## Configuration

| Method | Destination |
|---|---|
| `.WriteTo.Dlt(appId, socketPath, ...)` | dlt-daemon over Unix domain socket |
| `.WriteTo.DltTcp(appId, host, port, ...)` | dlt-daemon over TCP/IP |
| `.WriteTo.DltFile(path, appId, ...)` | rotating `.dlt` file with storage header |

All three accept `ecuId`, `defaultContextId`, `queueCapacity`, `shutdownTimeout`, `contextIdMap`, plus method-specific options (reconnect delays for daemon, file size/retention for file).

### `appsettings.json`

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "Dlt",     "Args": { "appId": "MYAP" } },
      { "Name": "DltFile", "Args": { "path": "logs/app.dlt", "appId": "MYAP" } }
    ]
  }
}
```

## How identifiers map

| DLT field | Source |
|---|---|
| `ECU` | `ecuId` option (default `"ECU1"`) |
| `APID` | `appId` option (required) |
| `CTID` | hashed from Serilog `SourceContext` (or `contextIdMap` override; or `defaultContextId` fallback) |
| Log level | mapped from Serilog level (Verbose->6, Debug->5, Information->4, Warning->3, Error->2, Fatal->1) |

## Behavior under load

The hot path enqueues onto a bounded channel (default capacity 10 000) and returns immediately. A background worker drains the queue and writes to the transport. On daemon outages a decorator reconnects with exponential backoff (500 ms -> 30 s, with jitter). If the queue overflows, the **oldest** event is dropped and a rate-limited `Serilog.Debugging.SelfLog` message reports the drop count.

## What's not in v1

- **Full bidirectional daemon ingestion.** `WriteTo.Dlt(...)` connects to
  `dlt-daemon` and successfully registers the application + contexts, but
  the daemon doesn't currently persist our LOG messages to its offline
  trace — `libdlt` is bidirectional (receiver thread drains daemon
  responses) and our transport is write-only. The file sink
  (`WriteTo.DltFile(...)`) is fully functional and produces output that
  `dlt-convert` / `dlt-viewer` read without issue. See `docker/README.md`
  for diagnostic details.
- DLT non-verbose mode
- Inbound DLT control messages (`SET_LOG_LEVEL` from daemon)
- TLS for TCP
- Multicast / UDP / serial transports

## License

MIT — see `LICENSE`.
