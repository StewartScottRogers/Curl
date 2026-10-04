---
id: BL-1292
title: Fail an RTSP reply whose Content-Length is over --max-filesize with exit 63
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: FR-084
created: 2026-10-02
completed: 2026-10-03
---
# BL-1292 — Fail an RTSP reply whose Content-Length is over --max-filesize with exit 63

## Goal

An `rtsp://` reply whose `Content-Length` is larger than `ITransferContext.MaxFileSize` ends the transfer with exit 63, `Maximum file size exceeded`, before any of its body is read, and closes the connection, as curl 8.21.0 does.

## Context

- Today `Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` reads the head (`RtspReplyReader.ReadHeadAsync`) and then always reads and discards the body (`RtspReplyReader.DiscardBodyAsync`, line 268); nothing reads `context.MaxFileSize`, and the connection goes back to the pool after a success.
- curl 8.21.0 parses an RTSP reply with the HTTP code: at the end of the head `lib/http.c` lines 3797-3804 fail a known size over the limit with `failf(data, "Maximum file size exceeded")` and `CURLE_FILESIZE_EXCEEDED` (exit 63); a `Content-Length` too large to parse fails the same way when a limit is set (lines 3245-3249). A limit of 0 is no limit, and a length equal to the limit passes.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\nhello'` and `-sv --max-filesize 3 rtsp://127.0.0.1:PORT/`: exit 63, stdout empty; stderr ends
  ```
  < RTSP/1.0 200 OK
  < CSeq: 1
  < Content-Length: 5
  * Maximum file size exceeded
  < 
  * closing connection #0
  ```
  with no `{ [5 bytes data]` line. Curl today reads the 5 bytes (`{ [5 bytes data]`) and ends `* shutting down connection #0` with exit 0.
- The `-v` line comes after the `Content-Length` line and before the head's empty `< ` line, so the check runs where `RtspReplyReader` reaches the end of the head, with the empty line still reported after the message.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Rtsp.UnitTests` drives the measured reply with `MaxFileSize = 3` and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Maximum file size exceeded`, the info and header lines in the measured order, no received-data event, no body byte read from the fake connection, and the connection closed (`closing connection #0`), not pooled.
- [x] A test with `Content-Length: 99999999999999999999` (overflowing) and `MaxFileSize = 3` asserts the same exit and message.
- [x] Tests pin that `MaxFileSize` of 0, `null` and exactly 5 end with exit 0 and the body read as today.
- [x] `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Rtsp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports no failing member.

## Notes

- Check how `RtspReplyHeadParser` treats an overflowing `Content-Length` today (`InvalidContentLength`) before writing the second test; if it already fails it another way without a limit, keep that path and only add the limit case.
- Measured 2026-10-03 (curl 8.21.0, Schannel) with `Record-CurlExchange.ps1`:
  - The overflowing case differs from a known length: `-sv --max-filesize 3` against `Content-Length: 99999999999999999999` exits 63 with `* Maximum file size exceeded` right after `< CSeq: 1`. The `Content-Length` line and the blank line are never reported. So `RtspReplyHeadParser` takes the limit and throws as the line is read (without a limit, the BL-840 path is unchanged: accepted, no body).
  - `-f` wins over the size: `RTSP/1.0 404` with `Content-Length: 5`, `-f --max-filesize 3` exits 22 with only `The requested URL returned error: 404`. So the size check runs after the `-f` check, before the blank line (`RtspProtocolHandler.SizeFailure`), and is pinned by a test.
- Exit 63 is not in `RtspVerboseLines`' keep or shut-down lists, so the connection reports `closing connection #0` and is never pooled, as measured.
- `RtspReplyReader.ReadHeadAsync`'s `log` parameter is now required. Its `?? new RtspTransferLog(...)` fallback was a branch no caller took, and it held branch coverage under 100% once the new parameter was added.
- The tests are in `Curl.Protocol.Rtsp.UnitTests/RtspProtocolHandlerMaxFileSizeTests.cs`. `ScriptedConnection.HasUnreadBytes` proves no body byte was read.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary -SkipTestRun` was run on the RTSP test project's own coverage (the full run measures the whole solution and outlasted 30 minutes). Result: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An rtsp reply whose Content-Length is over --max-filesize fails with exit 63 before its body is read, and closes the connection
