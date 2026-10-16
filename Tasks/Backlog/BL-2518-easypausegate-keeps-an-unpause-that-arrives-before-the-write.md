---
id: BL-2518
title: EasyPauseGate keeps an unpause that arrives before the write callback's pause is recorded
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2518 — EasyPauseGate keeps an unpause that arrives before the write callback's pause is recorded

## Goal

A `curl_easy_pause(CURLPAUSE_CONT)` made on another thread after a write callback has returned `CURL_WRITEFUNC_PAUSE`, but before `CallbackWriteStream.WriteAsync` has called `EasyPauseGate.Pause`, still resumes the transfer instead of leaving it paused forever.

## Context

Found by BL-2501 (ADR-0635). `Curl.Api`'s streamed response body (`Curl.Api/ResponseBodies/ResponseBodyBuffer.cs`) returns `WriteCallbackResult.Pause` from the write callback when its buffer is full and calls `IEasyTransfer.Pause(CurlPause.Continue)` from the reader's thread once the reader drains it. In `Curl.Engine.UnitLibrary/CallbackWriteStream.cs` `WriteAsync`, the callback returns `Pause`, then `gate.Pause(CurlPause.Receive)` sets the bit, then the transfer waits in `WhenResumedAsync`. If `ManagedEasyTransfer.Pause` (`EasyPauseGate.Set`) runs between the callback's return and `gate.Pause`, `gate.Pause` sets the receive bit again after the unpause and the transfer waits for an unpause that already happened. The same window exists for `CallbackReadStream` and `CurlPause.Send`. One fix: `EasyPauseGate` counts `Set` calls, the stream reads the count before calling the callback, and `Pause` ignores the request when a `Set` has happened since.

## Acceptance criteria

- [ ] A test in `Curl.Engine.UnitTests` unpauses from inside the window (for example from a write callback that calls `Pause(CurlPause.Continue)` on the handle before returning `WriteCallbackResult.Pause`) and the transfer completes with every byte delivered once.
- [ ] The same holds for a read callback returning `ReadCallbackResult.Pause`.
- [ ] `dotnet build` is clean and the fast tests pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
