---
id: BL-584
title: Write curl's -v and --trace lines for a WebSocket transfer
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-583]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-584 — Write curl's -v and --trace lines for a WebSocket transfer

## Goal

`-v`, `-i` and `--trace`/`--trace-ascii` on a `ws://` or `wss://` transfer write what curl 8.21.0 writes (the upgrade request and `101` head lines, the frame lines curl logs, closing lines), byte for byte apart from values that vary.

## Context

- Conformance audit 2026-09-28, row 36. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`; the HTTP handler's head reporting is the model for the upgrade exchange.
- Measure with `Record-CurlExchange.ps1`: `-v` for a text message then close, `-i` for the same, and `--trace-ascii -`.

## Acceptance criteria

- [ ] Measured first as above; stdout, stderr and trace output copied into Notes with varying parts (key, accept, ports) marked.
- [ ] `Curl.Console.UnitTests` pin the measured `-v` and `-i` output and the `--trace-ascii` dump for a fixed key.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
