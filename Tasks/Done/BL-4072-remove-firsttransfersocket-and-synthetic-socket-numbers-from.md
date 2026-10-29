---
id: BL-4072
title: Remove FirstTransferSocket and synthetic socket numbers from ManagedMultiTransfer and move its tests to real-socket answers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4036, BL-4037, BL-4038, BL-4070]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MultiEntry.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4072 — Remove FirstTransferSocket and synthetic socket numbers from ManagedMultiTransfer and move its tests to real-socket answers

## Goal

`ManagedMultiTransfer` no longer has `FirstTransferSocket` or synthetic socket numbers: a running transfer with no socket the handle knows is told no socket and is left out of `GetWaitSockets`.

## Context

Part 1 of 2 of BL-3990 (CGR-034), the code half of BL-4068. Drop `TellSocketCallback`'s `NeedsSocket` branch, `SocketNumberOf`, `nextTransferSocket`, `MultiEntry.Number` and `FirstTransferSocket` (and the `ResolverWakeupSocket` summary that cites it); `WaitSocketOf` answers `CurlSocket?` and nulls are skipped. Update the doc comments citing it in `ConnectOnlyConnection.cs` and `TransferSocketWait.cs`, and the assertion at `TransferSocketWaitTests.cs:106`. The 10 ms timer stays for BL-4069.

## Acceptance criteria

- [x] `FirstTransferSocket`, `SocketNumberOf`, `nextTransferSocket` and `MultiEntry.Number` no longer exist in `Curl.Engine.UnitLibrary`.
- [x] `GetWaitSockets` skips a running transfer with no known socket.
- [x] The socket expectations in `ManagedMultiTransferTests.SocketAction.cs`, `.Counts.cs` and `TransferSocketWaitTests.cs` expect the real-socket answers (no synthetic "In null" sockets) and pass.
- [x] `Cgr034Tests` still pin `C_20`, `C_74` and `C_75`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4068 (cost cap); BL-4068 and BL-3990's Notes hold the full plan.
- Continues the stashed work of BL-4068.
- The stash holds the whole production change and most test updates (see BL-4068's Notes for the list). Still failing when it was kept: `Counts.cs`'s `EnableNotification_AbortedByCallback_SendsEasyDoneForEachTransferItEnds`, which aborts through the socket callback a fake transfer is no longer told of: drive it over `ReadableWaitingTransfer`. BL-4070 (done) covers the Limits.cs and ManagedMultiTransferTests.cs tests.
- The ADR, the Engine CLAUDE.md paragraphs and the Measure-CodeQuality.ps1 run are BL-4073.
- Second claim: drove `EnableNotification_AbortedByCallback_SendsEasyDoneForEachTransferItEnds` and `Remove_SocketCallbackClearedSinceTheSocketWasGiven_RemovesTheTransfer` over `ReadableWaitingTransfer`'s real socket (a fake transfer is no longer told a socket); renamed Counts.cs's helper to `TransferHoldingSocketUntilCancelled` because `RealSockets.cs` (BL-4071) already has a `SocketHoldingTransfer` class.
- Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary first showed `GetWaitSockets` at complexity 12 (extracted `WaitedSockets`) and `RemoveEntry` at 87.5% branch (the cleared-callback Remove test above now reaches it); the rerun shows 100% line, 100% branch, 0 failing. Its first run also missed `MultiCallbackQueue.WaitUntilRun` lines 178-179, a thread-timing race outside this task's touches: filed as BL-4074.
- `Cgr034Tests` (C_20, C_74, C_75) pass. Linux and macOS: the changed tests use loopback-free wrapped sockets and no paths or OS text, so they are platform-neutral; fast tests green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c707c13f aaefc491 da38b818 18d43fff 779790c0.
- 2026-10-29: Doing -> Done. FirstTransferSocket and synthetic socket numbers removed; GetWaitSockets skips transfers with no known socket; tests expect real-socket answers; Engine library at 100/100 with 0 failing members.
