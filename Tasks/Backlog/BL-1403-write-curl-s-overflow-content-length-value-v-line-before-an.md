---
id: BL-1403
title: Write curl's 'Overflow Content-Length: value' -v line before an RTSP reply's too-large Content-Length header line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1403 — Write curl's 'Overflow Content-Length: value' -v line before an RTSP reply's too-large Content-Length header line

## Goal

An RTSP reply whose `Content-Length` number is too large for 64 bits writes curl 8.21.0's `* Overflow Content-Length: value` info line immediately before its `< Content-Length: ...` header line, as curl's shared HTTP header code does for RTSP; the rest of today's handling (no body read, exit 0, and exit 63 under `--max-filesize`) is unchanged.

## Context

- Today `Curl.Protocol.Rtsp.UnitLibrary/RtspReplyHeadParser.cs` (the `Content-Length:` branch around line 149) accepts such a number and leaves no body (BL-840), or fails with 63 `Maximum file size exceeded` under `--max-filesize` (BL-1292), but reports no line; `git grep "Overflow Content-Length" -- Curl.Protocol.Rtsp.UnitLibrary` finds nothing.
- curl 8.21.0 (tag `curl-8_21_0`): RTSP reply headers go through `Curl_http_header` in `lib/http.c`; lines 3243-3254, on `STRE_OVERFLOW` with no `max_filesize`, `streamclose(conn, "overflow content-length")` and `infof(data, "Overflow Content-Length: value")`, written before the header line itself is passed on to the trace.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`:
  - `-sv rtsp://127.0.0.1:PORT/a` against `RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\nhello`: `< RTSP/1.0 200 OK`, `< CSeq: 1`, `* Overflow Content-Length: value`, `< Content-Length: 99999999999999999999`, `< `, `* shutting down connection #0`; no data line, nothing on stdout; exit 0.
  - the same with `--max-filesize 10`: `< CSeq: 1`, `* Maximum file size exceeded`, `* closing connection #0`; exit 63 (already built, BL-1292).
  - `Content-Length: abc`: `< CSeq: 1`, `* Invalid Content-Length: value`, `* closing connection #0`; exit 8 (already built).
- The HTTP library gets the same line in its own task (BL-1387); the libraries do not share code, so this one is RTSP's.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Rtsp.UnitTests` drives the handler with the first measured reply and pins the `-v` sequence above, the info line directly before the `Content-Length` header line, nothing written and exit 0.
- [ ] The existing `--max-filesize` and `Content-Length: abc` tests pass unchanged (no overflow line in either).
- [ ] A reply with a second, equal overflowing `Content-Length` (`99999999999999999999, 99999999999999999999`) is pinned as curl reads it from the same source lines: one overflow line for the header line, no exit 8.
- [ ] `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Rtsp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports no failing member in the code this task changed.

## Log

- 2026-10-03: Created.
