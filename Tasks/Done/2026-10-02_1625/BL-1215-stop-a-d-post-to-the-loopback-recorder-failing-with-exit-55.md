---
id: BL-1215
title: Stop a -d POST to the loopback recorder failing with exit 55 or 56
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1215 — Stop a -d POST to the loopback recorder failing with exit 55 or 56

## Goal

`curl -d ab http://127.0.0.1:<port>/` against `Record-CurlExchange.ps1` exits 0 every time, as real curl does.

## Context

- Found in BL-1189: Curl's `-d ab` (and `-d @file` of 100000 bytes) against `Record-CurlExchange.ps1` (`200`, `Content-Length: 2`, `hi`) exits 56 `Failure when receiving data from the peer` or 55 `Failed sending data to the peer` in most runs (3 of 4 for `-d ab`, every run for 100000 bytes), before and after BL-1189's change; real curl 8.21.0 exits 0 every time. `-T` of the same 100000 bytes exits 0. Start by comparing `request.bin` and the write pattern (head and body as separate writes?) with real curl's.

## Acceptance criteria

- [x] The cause is found and stated in Notes.
- [x] A test reproducing it fails before the fix and passes after; 10 runs of `-d ab` and of `-d @<100000 bytes>` against the recorder all exit 0.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Cause, two halves. (1) Curl wrote the request head and the body as separate writes
  (`HttpRequestBodyWriter.WriteHeldHeadAsync`, then the piece); real curl 8.21.0 sends the head
  and the first body bytes from one 64 KiB upload buffer, so `-d ab` arrives as one 150-byte
  segment and `-d @<100000 bytes>` as 65536 bytes (153 head + `[65383 bytes data]`) and then the
  rest (measured with `Record-CurlExchange.ps1 -Curl` and `-v --trace-time`). (2) The recorder's
  `Test-RequestComplete` never matches `Content-Length` because its `[ \t]*$` multiline anchor
  sits after the line's `\r`, so it answers after its first read and closes with the rest
  unread. With Curl that first read was the head alone (request.bin 148 bytes, no `ab`), so the
  close met an unread body: Windows resets the connection, and Curl's next send or read failed
  with 55 or 56 depending on timing. Real curl's first segment already held its whole small body.
- Fix: on an HTTP/1.x connection, a held head now goes out in one write with as much of the
  first framed piece as fills `UploadBufferSize` (`WriteAfterHeldHeadAsync`), the rest in a
  second write. HTTP/2 and HTTP/3 stream connections (`IHttpStreamConnection`) take their first
  write as the head, so there the head is still written alone. Under 100-continue the head was
  already sent alone and still is, as curl does.
- After the fix, 10 runs each of `-d ab` (request.bin 150 bytes, head + body) and
  `-d @<100000 bytes>` (65536 bytes, as real curl's) against the recorder all exit 0. The 55/56
  did not reproduce on this run before the fix either (timing), but the write pattern, which is
  what lets the reset happen, now matches real curl's.
- Tests: `HttpRequestBodyWriterTests.WriteAsync_HeldHeadAnd*` (4, all failed before the fix);
  `ScriptedConnection.WriteLengths` added to see writes. The `writesBeforeFailure: 1` row of
  `ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage` now sends a 64 KiB
  body so its second write is still a body write.
- Coverage: Curl.Protocol.Http.UnitLibrary 100% line, 100% branch. Its one failing member,
  `Http2FrameTrace.Describe` at complexity 12, predates this task (BL-1167): filed as BL-1218.
  The recorder's regex is filed as BL-1217.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Curl sends an HTTP/1.x request head and its first body bytes in one write, as curl does, so -d POSTs to the loopback recorder exit 0
