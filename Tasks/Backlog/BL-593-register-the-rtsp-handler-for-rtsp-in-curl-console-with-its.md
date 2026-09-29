---
id: BL-593
title: Register the RTSP handler for rtsp in Curl.Console with its -v lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-592]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-593 — Register the RTSP handler for rtsp in Curl.Console with its -v lines

## Goal

`curl rtsp://...` runs end to end through `Curl.Console` with the options BL-590's ADR applies, and `-v`/`-i` write what curl 8.21.0 writes for an RTSP transfer.

## Context

- Conformance audit 2026-09-28, row 38. Handler: BL-591, BL-592.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `rtsp` with port 554; add it to the `-V` protocol list as ADR-0021 requires if that list is built here. Events: `ITransferEvents` (ADR-0046).
- Use BL-590's `-v` measurement; add `-i` if it is missing.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run `rtsp://127.0.0.1:<P>/media` (default and `-X DESCRIBE` or whatever the ADR records) through a fake connector, pinning request bytes, stdout, stderr, exit code and the `-v` lines.
- [ ] `curl -V` lists `rtsp`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 1): `-V`'s `Protocols:` line is the constant `CurlVersionText.ProtocolsLine` in
  `Curl.Cli.UnitLibrary` (pinned by `CurlVersionTextTests` in `Curl.Cli.UnitTests`, and compared in
  `Curl.Console.UnitTests/CurlCompositionWsTests.cs`), so the second criterion needs
  `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`; added to `touches`. BL-645 (in Doing) touches both,
  so the task went back to Backlog until BL-645 is Done. `ProtocolDispatcher` needs no change: it
  dispatches on each handler's `SupportedSchemes`, and the handler's default port 554 is already
  in `RtspProtocolHandler`. What remains: add `new RtspProtocolHandler(recordingConnector,
  httpAuthenticator)` to `CurlComposition.CreateProtocolHandlers`; emit ADR-0169's `-v` lines
  (`ReportRequestHeader`, `Request completely sent off`, `ReportResponseHeader`, `Connection #n ...
  left intact`, and the `-f` lines) from `RtspProtocolHandler`, as `WsProtocolHandler` does; add
  `rtsp` to `ProtocolsLine` between `pop3s` and `smtp`; ADR-0169 decision 5 (reuse across URLs with
  `CSeq: 0`) is filed separately if the pooled connector does not already give it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Cli.UnitLibrary and Curl.Cli.UnitTests for the -V Protocols line; BL-645 in Doing touches them
