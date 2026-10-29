---
id: BL-3990
title: Remove FirstTransferSocket and the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4033, BL-4034, BL-4035]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs]
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
- 2026-10-28 run (lane 6), before any code edit: the wiring of BL-3989/BL-4008 is in the tree, but the synthetic numbers and the 10 ms timer still carry three cases no task covers: (1) a transfer that is resolving has no told socket (TransferSocketWait has no resolve phase) - filed BL-4033; (2) `GetWaitSockets` (curl_multi_waitfds) and a handle without a socket callback have no `TransferSocketWait`, so no real socket to report - filed BL-4034; (3) a non-`ManagedEasyTransfer` (Curl.Api `StoreBoundEasyTransfer`, test fakes) gets no socket wait, and a transfer still computing after `SocketWaitSettleLimit` is only brought back by the timer - filed BL-4035. `ConnectOnlyConnection.cs`, `TransferSocketWait.cs` and `TransferSocketWaitTests.cs` also cite `FirstTransferSocket` (doc comments and one assertion); the removal must update them, so add them to `touches` on the next claim if no Doing task holds them.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 7440ff2a 34a4aed9 18d43fff 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-4033 (resolve-phase socket), BL-4034 (waitfds real sockets) and BL-4035 (non-managed transfers and settle-limit wakeup) before FirstTransferSocket and the 10 ms timer can go
