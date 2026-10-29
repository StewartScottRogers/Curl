---
id: BL-4069
title: Remove the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4072, BL-4073, BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4036, BL-4037, BL-4038]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs, Documentation/Decisions]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4069 — Remove the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034

## Goal

`ManagedMultiTransfer` no longer has the 10 ms `RunningTransferCheckInterval` timer: a running transfer with no socket makes the timer 0, as libcurl's `Curl_expire(data, 0)`.

## Context

Part 2 of 2 of BL-3990 (CGR-034); runs after BL-4068. Remove `RunningTransferCheckInterval`; `CheckIntervalMilliseconds` returns 0 for such a transfer. Update the summaries in `ManagedMultiTransfer.cs` (class summary, field) and `.Timer.cs`, the expectations "timer 10" to "timer 0" and socket actions on `FirstSocket` to `CurlMultiSockets.Timeout` in `SocketAction.cs`, `Timers.cs` line 267 and `Cgr148Tests.cs` line 17, and the Curl.Engine.UnitLibrary CLAUDE.md paragraphs BL-2426 and BL-2427. Record the decision in a new ADR (Decided by Claude under Stewart's delegation).

## Acceptance criteria

- [x] `RunningTransferCheckInterval` no longer exists in `Curl.Engine.UnitLibrary`.
- [x] The tests in `ManagedMultiTransferTests.SocketAction.cs`, `.Timers.cs` and `Cgr148Tests.cs` expect the real-socket answers and timer 0, and pass.
- [x] A new ADR records the decision, marked "Decided by Claude under Stewart's delegation", and the Curl.Engine.UnitLibrary CLAUDE.md no longer describes the synthetic numbering.
- [x] `Cgr034Tests` still pin `C_20`, `C_74` and `C_75`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3990 (cost cap); BL-3990's Notes hold the full plan and decision.
- 2026-10-29 run (lane 2): added `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs` to `touches`: `IsActionFor`'s summary cites `RunningTransferCheckInterval`, so the removal does not compile without it; no task in Doing on origin/work/dark-factory names it (checked).
- Decision (ADR-0998): `CheckIntervalMilliseconds` and `IsComputing` are gone rather than kept answering 0: with the interval 0, `Math.Min(handleTimeout, 0)` and the computing case both give 0, so `RunningTimerMilliseconds` answers the earliest deadline when every running transfer waits on its socket, else 0. ADR-0997's status line notes that ADR-0998 replaces its timer point. `Timers.cs` line 267 needed no change (it already names `TransferSocketWait.FirstEngineSocket`, BL-4072), and no socket action in `SocketAction.cs` or `Cgr148Tests.cs` names `FirstSocket` any more.
- Fixed four Engine tests the change touched beyond the plan: `Cgr148Tests`' two perform-driven sequences now read `timer 0, timer -1` (perform reports the timer only when it changes, and it stays 0), the perform-abort test drives its second timer call with no socket callback and a 4 ms transfer timeout (the timer is then that timeout), and `SocketAction.cs`'s transfer-timeout test is now `SocketActionAsync_RunningTransferWithATimeoutAndNoSocket_MakesTheTimerZero`.
- Verified: full fast tests run once (only Curl.Engine.UnitTests failed, the four above); after the fix Curl.Engine.UnitTests 2912 passed, 0 failed; `Cgr034Tests` (the CGR-034 pins C_20, C_74, C_75) unchanged and passing. Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members. CI on Linux and macOS: the change is platform-neutral (timer values only, no paths or OS text).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0c6694c6 7953220b 3b4ad1dc 18d43fff 779790c0.
- 2026-10-29: Doing -> Done. ManagedMultiTransfer has no 10 ms RunningTransferCheckInterval: a running transfer with no known socket makes the timer 0 (ADR-0998)
