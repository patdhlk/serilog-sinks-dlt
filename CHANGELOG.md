# Changelog

All notable changes to this project will be documented in this file. The format is loosely based on [Keep a Changelog](https://keepachangelog.com), and this project adheres to [Semantic Versioning](https://semver.org).

## [Unreleased]

## [0.1.0] - 2026-05-20

### Added

Initial release. End-to-end verified against COVESA `dlt-daemon` v3.0.0.

- **Three sinks:**
  - `WriteTo.Dlt(appId, socketPath, ...)` — daemon over Unix domain socket
  - `WriteTo.DltTcp(appId, host, port, ...)` — daemon over TCP/IP
  - `WriteTo.DltFile(path, appId, ...)` — rotating `.dlt` file with DLT storage header
- **Verbose-mode DLT v1 encoder** with typed arguments (string / int8-64 / uint8-64 / float32-64 / bool / raw bytes). Output decodes correctly under `dlt-convert -a` and `dlt-viewer`.
- **libdlt-compatible daemon framing**: `DltUserHeader` prefix on every message, `REGISTER_APPLICATION` on connect, `REGISTER_CONTEXT` for each unique CTID, HTYP with `WSID | WEID | WTMS | UEH | VERS=1` matching libdlt v2.18+.
- **Bidirectional transport** — background read loop drains daemon-sent control messages so the daemon's send-side never stalls.
- **Channel-based dispatcher** with bounded queue (default 10 000), drop-oldest under backpressure, rate-limited drop reporting via `Serilog.Debugging.SelfLog`.
- **Reconnecting decorator** with exponential backoff + jitter for the daemon transports.
- **File sink** with size-based rotation and configurable retention count.
- **`SourceContext` → CTID** mapping (FNV-1a base32 hash) with optional explicit `contextIdMap` overrides.
- **Docker dev container** that builds `dlt-daemon` from source with Unix-socket IPC, plus `demo.sh` end-to-end smoke test that drops result files into `.demo-output/` for opening in DLT Viewer.
- **CI** on Ubuntu / Windows / macOS for unit tests, plus a Linux job that runs the integration test against the running daemon inside the dev container.

[Unreleased]: https://github.com/patdhlk/serilog-sinks-dlt/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/patdhlk/serilog-sinks-dlt/releases/tag/v0.1.0
