---
id: BL-2729
title: Receive the response while a callback upload is paused and stop or keep sending on an early error response
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2728]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2729 — Receive the response while a callback upload is paused and stop or keep sending on an early error response

## Goal

While a streamed request body's read waits on a paused upload (`CURL_READFUNC_PAUSE`), the HTTP handler keeps reading the response and delivers it; on an early response of 300 or above it stops sending and closes the connection, or with `HttpRequestOptions.KeepSendingOnError` it sends the rest of the body once the upload is unpaused and keeps the connection, as libcurl 8.x does (upstream case 1533).

## Context

Found by BL-2646. Upstream case 1533 (`Curl.Conformance.UnitTests/UpstreamTestData/test1533.curltest`, program `Curl.Conformance.LibTest.UnitLibrary/Lib1533.cs`): a 3-byte POST whose read callback pauses until the write callback sees the response, and the write callback then unpauses with `curl_easy_pause(CURLPAUSE_CONT)`. The server answers `401` with `Content-Length: 15` before the body. Today `HttpRequestBodyWriter.WriteAsync` (`Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs`) awaits the body stream's read before reading any response, so the paused read never resumes and the transfer hangs (20 s runner timeout). The stream's `ReadAsync` completes synchronously for an unpaused callback and stays pending only while paused (`Curl.Engine.UnitLibrary/CallbackReadStream.cs`, `EasyPauseGate.cs`), so racing the pending read against reading the response head is one way in. Expected wire bytes from the case: requests 1 and 2 (no keep-sending) send headers only and each opens a new connection; requests 3 and 4 (keep-sending) send `aaa` after the headers, and request 4 reuses request 3's connection. The existing `EarlyResponseWatch` (`HttpContinueWaitConnection`) watches for a status of 300 or above during a send after `Expect: 100-continue`, and `HttpConnectionInfoLines.KeepSendingToTossAway` exists already. BL-2646 then maps the option in the engine and puts case 1533 on the ratchet.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` shows a POST whose body stream read stays pending until the response is delivered: without `KeepSendingOnError`, the response is delivered, no body byte is sent and the connection is not kept.
- [ ] A test in `Curl.Protocol.Http.UnitTests` shows the same POST with `KeepSendingOnError`: the response is delivered, the body is sent once the read completes, and the connection is kept for reuse.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-18: Created.
