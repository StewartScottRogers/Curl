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
completed: 2026-09-29
---
# BL-593 — Register the RTSP handler for rtsp in Curl.Console with its -v lines

## Goal

`curl rtsp://...` runs end to end through `Curl.Console` with the options BL-590's ADR applies, and `-v`/`-i` write what curl 8.21.0 writes for an RTSP transfer.

## Context

- Conformance audit 2026-09-28, row 38. Handler: BL-591, BL-592.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `rtsp` with port 554; add it to the `-V` protocol list as ADR-0021 requires if that list is built here. Events: `ITransferEvents` (ADR-0046).
- Use BL-590's `-v` measurement; add `-i` if it is missing.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` run `rtsp://127.0.0.1:<P>/media` (default and `-X DESCRIBE` or whatever the ADR records) through a fake connector, pinning request bytes, stdout, stderr, exit code and the `-v` lines.
- [x] `curl -V` lists `rtsp`, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

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
- 2026-09-29 (lane 3): Done. `CurlComposition` registers `RtspProtocolHandler` with the shared
  `httpAuthenticator`; `-V` lists `rtsp` between `pop3s` and `smtp`. ADR-0169 decision 5 needed no
  new task: the handler sets `ConnectTarget.PoolScheme = "rtsp"`, calls `MarkReusable` when curl
  leaves the connection intact, and starts `CSeq` at 0 when `ConnectResult.IsReused`, so the
  `PoolingConnector` already gives the reuse and its `Reusing existing rtsp:` line
  (`CurlCompositionRtspTests.CreateRunner_TwoUrlsOnOnePooledConnection_...`).
- `-v` measured with `Record-CurlExchange.ps1` on curl 8.21.0 mingw (ports 47971-47982), beyond
  ADR-0169's rows 12 and 17: the request is `> ` lines then `Request completely sent off`; head lines
  `< `; the `-f` message comes before the head's blank line, `Unsupported response code in HTTP
  response` (status 099, exit 1) after it; both then `closing connection #0`. A body is
  `{ [N bytes data]` and then `shutting down connection #0` (even with the server holding the
  connection open); `Content-Length: 0` or no body gives `left intact`. A `CSeq` mismatch (also
  after a head cut short, whose unread `CSeq: 1` line is still written as `< CSeq: 1`) writes its
  message and `left intact`. A non-RTSP reply writes `Empty reply from server` and `shutting down`.
  Two `Session` IDs in one reply: head lines up to the first, the 86 message, `closing`. `-H CSeq:`
  and `-H Session:` refusals: the message and `left intact`, no `>` lines. Header lines on Windows
  end `\r\r\n` in real curl, as in ours.
- Unmeasured, chosen by rule (Decided by Claude under Stewart's delegation): a `CSeq` mismatch
  with a body shuts the connection down, as any body does; a send, receive, header-write or
  head-too-large failure writes its message and `closing`, as curl's other failures do.
  Recorded here rather than in ADR-0169 because `Documentation/` is outside this task's `touches`.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Cli.UnitLibrary and Curl.Cli.UnitTests for the -V Protocols line; BL-645 in Doing touches them
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. curl rtsp:// runs through Curl.Console with curl 8.21.0's -v lines, connection reuse with CSeq 0, and -V lists rtsp
