---
id: BL-3990
title: Remove FirstTransferSocket and the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007]
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

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 7440ff2a 34a4aed9 18d43fff 779790c0.
