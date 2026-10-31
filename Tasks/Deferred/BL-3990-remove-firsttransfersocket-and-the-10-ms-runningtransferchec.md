---
id: BL-3990
title: Remove FirstTransferSocket and the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4036, BL-4037, BL-4038]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3990 — Remove FirstTransferSocket and the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034

## Goal

`ManagedMultiTransfer` no longer uses synthetic `FirstTransferSocket` numbers or the 10 ms `RunningTransferCheckInterval` timer; every transfer has a socket the application is told of.

## Context

Split from BL-3976 (cost cap), itself split from BL-3563 (CGR-034). BL-3981 landed `TransferSocketWait.TakeSocket` and `SignalOn`, so connections with no OS socket (test-fake transfers, `Lib530Run`, `Lib582Run`) have a socket number; BL-3989 wires the multi handle to them. Fake-transfer tests in `ManagedMultiTransferTests.SocketAction.cs`, `.Counts.cs`, `.Timers.cs` and `Cgr148Tests.cs` still expect the synthetic numbers and the 10 ms timer.

## Acceptance criteria

- [ ] `FirstTransferSocket` and `RunningTransferCheckInterval` (10 ms) no longer exist in `Curl.Engine.UnitLibrary`.
- [ ] The tests in `ManagedMultiTransferTests.SocketAction.cs`, `.Counts.cs`, `.Timers.cs` and `Cgr148Tests.cs` expect the real-socket answers and pass.
- [ ] `Cgr034Tests` still pin `C_20`, `C_74` and `C_75`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3976 (cost cap); runs after BL-3989.
- 2026-10-28 run (lane 6), before any code edit: the wiring of BL-3989/BL-4008 is in the tree, but the synthetic numbers and the 10 ms timer still carry three cases no task covers: (1) a transfer that is resolving has no told socket (TransferSocketWait has no resolve phase) - filed BL-4036; (2) `GetWaitSockets` (curl_multi_waitfds) and a handle without a socket callback have no `TransferSocketWait`, so no real socket to report - filed BL-4037; (3) a non-`ManagedEasyTransfer` (Curl.Api `StoreBoundEasyTransfer`, test fakes) gets no socket wait, and a transfer still computing after `SocketWaitSettleLimit` is only brought back by the timer - filed BL-4038. `ConnectOnlyConnection.cs`, `TransferSocketWait.cs` and `TransferSocketWaitTests.cs` also cite `FirstTransferSocket` (doc comments and one assertion); the removal must update them, so add them to `touches` on the next claim if no Doing task holds them.
- 2026-10-29 run (lane 1): BL-4036/4037/4038 are Done. No task in Doing on origin/work/dark-factory holds `ConnectOnlyConnection.cs`, `TransferSocketWait.cs` or `TransferSocketWaitTests.cs` (checked); add them to `touches` (doc comments citing `FirstTransferSocket`, and `TransferSocketWaitTests.cs:106`'s assertion). The run reverted its edits and requeued at about $1.3 of the cap, before the tests, so the next claim starts on the edits with this plan:
  - What still uses the synthetic numbers and the 10 ms timer: only a transfer whose `PerformingTransfer` is not a `ManagedEasyTransfer` (test fakes; production always forwards one since ADR-0986), plus a managed one before its dial has a socket, and one waiting without a socket.
  - Decision to record in a new ADR (decided by Claude under Stewart's delegation): a running transfer with no socket the handle knows is told no socket (drop `TellSocketCallback`'s `NeedsSocket` branch, `SocketNumberOf`, `nextTransferSocket`, `MultiEntry.Number`), is left out of `GetWaitSockets` (`WaitSocketOf` answers `CurlSocket?`, nulls skipped), and makes the timer 0 (`CheckIntervalMilliseconds` returns 0: `Math.Min(handleTimeout, 0)` is always 0 there), as libcurl's `Curl_expire(data, 0)` for a transfer that can run on. Real libcurl has no transfer without a socket, so nothing to measure.
  - Files: `ManagedMultiTransfer.cs` (class summary lines 27-29, field line 70, `FirstTransferSocket` lines 82-87, `ResolverWakeupSocket` summary "just below FirstTransferSocket", `RunningTransferCheckInterval` lines 95-100), `.Timer.cs` (summary line 57, lines 80-89), `.MovingOn.cs` (lines 181-185, 218, `IsActionFor` summary line 223), `.WaitSockets.cs` (summary line 108, line 126), `.Queueing.cs` lines 67-69 (add to touches), the three doc comments above, and the fake-transfer tests: `SocketAction.cs` (expected "socket ... In null" lines go, "timer 10" becomes "timer 0", socket actions on `FirstSocket` become `CurlMultiSockets.Timeout`), `Counts.cs` lines 75-125, `Timers.cs` line 267, `Cgr148Tests.cs` line 17, and the Curl.Engine.UnitLibrary CLAUDE.md paragraphs BL-2426 and BL-2427 that describe the numbering.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 7440ff2a 34a4aed9 18d43fff 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-4036 (resolve-phase socket), BL-4037 (waitfds real sockets) and BL-4038 (non-managed transfers and settle-limit wakeup) before FirstTransferSocket and the 10 ms timer can go
- 2026-10-29: Backlog -> Doing. Criteria at claim: 7440ff2a 34a4aed9 18d43fff 779790c0.
- 2026-10-29: Doing -> Backlog. Cost cap: the plan and decisions are under Notes; the next claim makes the edits and test updates
- 2026-10-29: Backlog -> Deferred. Split into BL-4068 and BL-4069 (cost cap)
