---
id: BL-3206
title: Block in curl_multi_poll and curl_multi_wait when a handle has a timeout set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Libcurl/Multi/Wait/CurlMulti.Wait.cs, Curl.Engine.UnitTests/MultiPollTimeoutWaitTests.cs, Libcurl.UnitTests/Multi/Wait/CurlMultiPollBusyWaitTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3206 — Block in curl_multi_poll and curl_multi_wait when a handle has a timeout set

## Goal

`PollAsync` and `WaitAsync` block up to their timeout (or the next handle timer, whichever is sooner) when nothing is ready, also when a handle has a timeout option set.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 27). Repro: add one easy handle for a slow URL with TimeoutMs=300 (or any timeout option), then loop Perform then PollAsync(..., 100). Upstream blocks inside poll; ours returns at once each time, so the loop spins hundreds of times a second at full CPU until the timeout fires. Without a timeout option the poll waits correctly, so the likely cause is the handle's timer being reported as already due (or as a zero wait) to the poll. Use the injected `TimeProvider` (fake clock) where the test can; a loopback server that holds the response is fine.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [x] `Libcurl.UnitTests/Multi/Wait/CurlMultiPollBusyWaitTests.cs` counts Perform/PollAsync(100) loop iterations over a 1-second wait with TimeoutMs set and expects about 10 or fewer (allow a small margin), not hundreds; the same for WaitAsync.
- [x] A test shows the poll still returns early when the handle's timeout is due sooner than the poll timeout, and the transfer then fails with exit 28.
- [x] `Curl.Engine.UnitTests/MultiPollTimeoutWaitTests.cs` pins the engine's computed wait for a handle with a timeout.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes
- Reproduced 2026-10-22 against the real engine and a loopback server that never answers (file-based app, not committed): with `CURLOPT_TIMEOUT_MS` 3000 the Perform/PollAsync(100) loop turned every ~110 ms (correct); with 300 it turned every ~110 ms until the timeout passed, then more than 100,000 times in the ~140 ms until the perform's own max-time watchdog ended it with `OperationTimedOut`. Cause: `GetTimeout` answers 0 once a running transfer's timeout has passed (as `curl_multi_timeout` does), and `WaitLimit` capped the wait with it, so every wait returned at once until the perform ended.
- Fix (ADR-0783): the wait caps itself with the new `ManagedMultiTransfer.WaitTimerMilliseconds`, which leaves out a timeout already passed; the wait already ends when a perform finishes, so it now ends when the transfer's perform ends with exit 28, or at the caller's timeout. `GetTimeout` is unchanged. Forcing exit 28 from the multi handle at the timer was rejected: it would drop the perform's error text and info.
- The pinned values are the engine's computed wait, not libcurl output text, so no libcurl 8.22.0 measurement was needed to pin them (the task's "measure upstream" step); the behaviour pinned is libcurl's documented one, poll blocking up to its timeout or the next timer.
- `CurlMultiPollBusyWaitTests` runs on the system clock (about 1.3 s each) over a fake transfer that ends with exit 28 100 ms after its timeout, as the engine's watchdog does, and caps the loop at 1000 turns so the old behaviour fails rather than hangs; it allows up to 20 turns for timer granularity on CI. `MultiPollTimeoutWaitTests` uses `TimerManualTimeProvider`. Both are platform-neutral (no paths, sockets or OS text).
- Added `Curl.Engine.UnitLibrary/CLAUDE.md` to touches: its BL-2425 section said the wait is capped by `GetTimeout`, no longer true; no task in Doing on origin/work/dark-factory names it.
- The timer callback (`CURLMOPT_TIMERFUNCTION`) still reports 0 after a timeout passes until the perform ends; left as it is (not in this task's scope; its report is `GetTimeout`'s, as libcurl's).
- Fast tests (whole solution, TestCategory!=Integration) green, build with -warnaserror clean, Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary 0 failing members (Curl.Engine.UnitLibrary is the only production library changed). CI on Linux and macOS: the change is platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 29f23036 fa7d51cf 4e75726c 3639a5e6.
- 2026-10-23: Doing -> Done. curl_multi_poll and curl_multi_wait block until the transfer's perform ends once its CURLOPT_TIMEOUT has passed, so a perform-and-poll loop no longer spins
