---
id: BL-3832
title: curl_easy_pause(CURLPAUSE_SEND) holds the read callback until unpaused
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/CallbackReadStream.cs, Curl.Engine.UnitTests/CallbackReadStreamPauseByApiTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3832 — curl_easy_pause(CURLPAUSE_SEND) holds the read callback until unpaused

## Goal

A read callback is not called while the application has paused sending with `curl_easy_pause(CURLPAUSE_SEND)`, and is called again once it unpauses, as libcurl 8.22.0 holds the upload while `CURLPAUSE_SEND` is set.

## Context

BL-3564 (CGR-035) made `CallbackWriteStream.WriteAsync` wait on the handle's `EasyPauseGate` before calling the write callback when receiving is paused through `curl_easy_pause`. `CallbackReadStream.ReadAsync` (`Curl.Engine.UnitLibrary/CallbackReadStream.cs`, around line 138) still waits only after the read callback itself returns `CURL_READFUNC_PAUSE`, so a `curl_easy_pause(e, CURLPAUSE_SEND)` from the application (or from another callback) does not hold the upload. The same one-line pre-wait (`pauseGate?.WhenResumedAsync(CurlPause.Send, cancellationToken)`) before the first callback call is the likely fix. Measure libcurl's behaviour before pinning anything; check upstream tests that pause the send side (e.g. test1533, BL-2646's `ManagedEasyTransferTests.PausedPost.cs`) still pass.

## Acceptance criteria

- [ ] `CallbackReadStream.ReadAsync` calls no read callback while the gate has `CurlPause.Send` set, and calls it once the gate is cleared; a focused test in `Curl.Engine.UnitTests/CallbackReadStreamPauseByApiTests.cs` fails before the change and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
