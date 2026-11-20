---
id: BL-5578
title: SignalR keep-alive tests wait on observable state, not Task.Delay(50)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5578 — SignalR keep-alive tests wait on observable state, not Task.Delay(50)

## Goal

The keep-alive tests BL-5575 added order their steps by waiting on observable state, so they cannot flake on a loaded CI runner and always exercise the branch they name.

## Context

Found by the shift-end review of 2026-11-19 (code-reviewer) on the BL-5575 change (commit 9e3ea4532a), in `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRKeepAliveTests.cs`:

- `PingWriteFailsAfterTheServersCloseArrived_DoesNotChangeTheExitCode_HubProtocolPing` gives the server's Close, sleeps `Task.Delay(50)`, then fails the held Ping write. `CloseReceived` is set only once the read loop has decoded the Close. On a slow Linux or macOS runner the read loop may not have run within 50 ms. The `IOException` then escapes `TrySendPingAsync` and the transfer ends `RecvError`, not `Ok`, so the test is flaky. BL-5577 may remove that outcome in production, but the test should still not depend on timing.
- `PingQueuedBehindTheServersCloseAnswer_IsDroppedNotWrittenAfterTheClose_HubProtocolPing` uses `Task.Delay(50)` after `clock.Advance` and does not prove the Ping loop reached the send semaphore before `ReleaseHeldWrite`. If the Ping loop is late, the transfer ends and cancels it first: `CountPings == 0` still holds, but the `dropOnceClosing && closeFrameSent` branch in `SignalRWebSocketSession.SendFrameAsync` never runs. The test name then lies, and the 100% branch coverage gate depends on timing.
- `ReadLoopFailsWhileAPingWriteIsBlocked_WaitsForThePingThenReportsTheReadFailure_HubProtocolPing` asserts `IsFalse(run.IsCompleted)` after a 50 ms delay, which holds whether or not the read failure was seen yet.

## Acceptance criteria

- [ ] None of the three tests above uses `Task.Delay` for ordering; each waits (e.g. with the file's `WaitUntil`) on an observable fact - the Close frame consumed, a second write pending at the semaphore, the read failure observed.
- [ ] `PingQueuedBehindTheServersCloseAnswer_...` provably reaches the drop branch: it fails if `SendTextUnlessClosingAsync` writes instead of dropping (check by temporarily removing the `dropOnceClosing` guard).
- [ ] The three tests pass 50 runs in a row (`dotnet test --filter "FullyQualifiedName~SignalRKeepAliveTests"` in a loop).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created.
