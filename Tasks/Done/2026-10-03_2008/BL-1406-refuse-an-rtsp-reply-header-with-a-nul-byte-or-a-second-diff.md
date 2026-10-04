---
id: BL-1406
title: Refuse an RTSP reply header with a NUL byte or a second different Location with curl's exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1403]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1406 — Refuse an RTSP reply header with a NUL byte or a second different Location with curl's exit 8

## Goal

An RTSP reply header line holding a NUL byte fails with exit 8 `Nul byte in header`, and a second `Location` header that differs from the first fails with exit 8 `Multiple Location headers`, each before the refused line is written, as curl 8.21.0's shared HTTP header code refuses them for RTSP.

## Context

- Today `Curl.Protocol.Rtsp.UnitLibrary/RtspReplyHeadParser.cs` refuses a header without a colon, a carriage return inside a line and bad `CSeq`/`Content-Length` values (its remarks list them), but neither text above exists in the RTSP library, so such a reply is accepted.
- curl 8.21.0 (tag `curl-8_21_0`): RTSP replies are parsed by `lib/http.c` (`http_rw_hd`, `Curl_http_header`), which refuses a header line containing a NUL byte (`Nul byte in header`) and, for a second `Location:` whose value differs from the stored one, `Multiple Location headers`, both `CURLE_WEIRD_SERVER_REPLY`. The HTTP library implemented the same two checks in BL-1331 (`Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs` `NulByteInHeader`, `MultipleLocationHeaders`); read their exact conditions and order from `lib/http.c` and BL-1331's tests, since the libraries cannot share code.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`, `-sv rtsp://127.0.0.1:PORT/a`:
  - `RTSP/1.0 200 OK\r\nCSeq: 1\r\nX-A: a\0b\r\nContent-Length: 0\r\n\r\n`: `< RTSP/1.0 200 OK`, `< CSeq: 1`, `* Nul byte in header`, `* closing connection #0`; exit 8.
  - `RTSP/1.0 302 Found\r\nCSeq: 1\r\nLocation: /x\r\nLocation: /y\r\nContent-Length: 0\r\n\r\n`: `< CSeq: 1`, `< Location: /x`, `* Multiple Location headers`, `* closing connection #0`; exit 8.
- BL-1403 changes the same parser first; build on it.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Rtsp.UnitTests` drive the handler with both measured replies and pin the `-v` lines, exit 8 (`CurlExitCode.WeirdServerReply`) and message, with the refused line not written.
- [x] Tests pin that two equal `Location` headers are accepted and that a NUL on the status line is handled as `lib/http.c` handles it (measure it with `Record-CurlExchange.ps1` and pin the result).
- [x] `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Rtsp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), Record-CurlExchange.ps1, `-sv rtsp://127.0.0.1:PORT/a`:
  - NUL after a valid status line start (`RTSP/1.0 200 O\0K`): `* Nul byte in header`, exit 8, nothing written. NUL before the status line's blank (`RTSP/1.0\0 200 OK`): fails the status check first, exit 8 (Weird server reply).
  - NUL is checked before the carriage return check on both the status line and header lines (`O\0\rK`, `O\rK\0`, `a\0\rb` all report the NUL), and before the colon check (`X-A a\0b`).
  - `Location: /x` then `Location:  /x ` is accepted (values compared after trimming blanks); `Location:` (empty) then `/y` then `location: /z` fails at the `/z` line: an empty value is ignored and the name matches in any case.
- Implemented in `RtspReplyHeadParser`: `RefuseNulByte` runs after the status check and before the carriage return and colon checks; `KeepLocation` mirrors HTTP's BL-1331 rule.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An RTSP reply header with a NUL byte or a second different Location fails with exit 8 like curl 8.21.0
