---
id: BL-3206
title: Block in curl_multi_poll and curl_multi_wait when a handle has a timeout set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Libcurl/Multi/Wait/CurlMulti.Wait.cs, Curl.Engine.UnitTests/MultiPollTimeoutWaitTests.cs, Libcurl.UnitTests/Multi/Wait/CurlMultiPollBusyWaitTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3206 — Block in curl_multi_poll and curl_multi_wait when a handle has a timeout set

## Goal

`PollAsync` and `WaitAsync` block up to their timeout (or the next handle timer, whichever is sooner) when nothing is ready, also when a handle has a timeout option set.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 27). Repro: add one easy handle for a slow URL with TimeoutMs=300 (or any timeout option), then loop Perform then PollAsync(..., 100). Upstream blocks inside poll; ours returns at once each time, so the loop spins hundreds of times a second at full CPU until the timeout fires. Without a timeout option the poll waits correctly, so the likely cause is the handle's timer being reported as already due (or as a zero wait) to the poll. Use the injected `TimeProvider` (fake clock) where the test can; a loopback server that holds the response is fine.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Libcurl.UnitTests/Multi/Wait/CurlMultiPollBusyWaitTests.cs` counts Perform/PollAsync(100) loop iterations over a 1-second wait with TimeoutMs set and expects about 10 or fewer (allow a small margin), not hundreds; the same for WaitAsync.
- [ ] A test shows the poll still returns early when the handle's timeout is due sooner than the poll timeout, and the transfer then fails with exit 28.
- [ ] `Curl.Engine.UnitTests/MultiPollTimeoutWaitTests.cs` pins the engine's computed wait for a handle with a timeout.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
