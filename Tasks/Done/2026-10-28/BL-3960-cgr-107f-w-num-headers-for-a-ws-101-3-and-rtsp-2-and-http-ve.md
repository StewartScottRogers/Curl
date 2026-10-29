---
id: BL-3960
title: CGR-107f: -w num_headers for a ws 101 (3) and RTSP (2), and http_version 1.1 for RTSP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs, Curl.Protocol.Ws.UnitTests/Cgr107Tests.cs, Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs, Curl.Protocol.Rtsp.UnitTests/Cgr107Tests.cs, Curl.Protocol.Rtsp.UnitLibrary/RtspReplyHead.cs, Curl.Protocol.Rtsp.UnitLibrary/RtspReplyHeadParser.cs, Curl.Protocol.Rtsp.UnitLibrary/RtspReplyReader.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3960 — CGR-107f: -w num_headers for a ws 101 (3) and RTSP (2), and http_version 1.1 for RTSP

## Goal

`%{num_headers}` counts a ws 101's and an RTSP response's header lines, and `%{http_version}` is 1.1 for RTSP, as curl 8.22.0 does.

## Context

Split from BL-3636 (third-party gap report issue CGR-107, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3636's Context for the repro setups and commands). Measure real curl 8.22.0 with `Record-CurlExchange.ps1` or a C# file-based loopback server (no Python) before pinning a value. OP-11: `-o NUL -w "%{num_headers}" ws://...` prints 3 with curl (101 response) and 0 with Curl; `-w "%{num_headers} %{http_version}" rtsp://127.0.0.1:PORT/m` prints `2 1.1` with curl and `0 0` with Curl. The handlers leave the report's header count and HTTP version unset.

## Acceptance criteria

- [x] Both repros print what real curl prints.
- [x] A focused unit test in the fixed library's own `.UnitTests` project (the file in `touches`) fails before the fix and passes after it (ADR-0482).
- [x] The real-curl and Curl output of each repro is recorded in Notes and matches, platform-neutral (Windows and Linux answers pinned separately where they differ).
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed library shows no member outside a gate.

## Notes

- Measured with Record-CurlExchange.ps1, 2026-10-28, real curl 8.22.0 (Schannel) and Curl, identical stdout and exit code:
  - ws 101 (Upgrade, Connection, Sec-WebSocket-Accept), `-sS -o NUL -w "%{num_headers} %{http_version} %header{upgrade}" ws://127.0.0.1:PORT/x`: `3 1.1 ` exit 52 from both (before the fix Curl printed 0 for num_headers).
  - rtsp `RTSP/1.0 200 OK` + CSeq + Content-Length, `-sS -w "%{num_headers} %{http_version} %header{cseq}" rtsp://127.0.0.1:PORT/m`: `2 1.1 ` exit 0 from both (before: `0 0`).
  - rtsp with CSeq, Session, X-A, Content-Length: `4 1.1 ` exit 0 from both.
- Decision (measured, so no new ADR): curl counts these lines in `%{num_headers}` but `%header{}` finds none of them, so the handlers report them as `TransferReport.PseudoHeaders` (ADR-0052's counted-but-not-findable headers), not `ResponseHeaders`. RTSP reports `HttpVersion` 1.1.
- Added RtspReplyHead.cs, RtspReplyHeadParser.cs and RtspReplyReader.cs to touches: the handler never sees the raw head, so the parser collects the header lines and the head carries them. No task in Doing on origin/work/dark-factory names an RTSP or WS file.
- The Cgr107Tests in both test projects fail before the fix (PseudoHeaders empty, RTSP HttpVersion null) and pass after.
- Platform-neutral: loopback fakes only, no paths or OS text; the Linux/macOS criterion is met by that and the green fast run here.
- dotnet build clean, fast tests green, Measure-CodeQuality.ps1 for Curl.Protocol.Ws.UnitLibrary and Curl.Protocol.Rtsp.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 83d17d63 d9aa5d84 40bddbd7 bc4b41f1.
- 2026-10-28: Doing -> Done. ws 101 and RTSP report their header lines for %{num_headers} and RTSP reports http_version 1.1, matching curl 8.22.0
