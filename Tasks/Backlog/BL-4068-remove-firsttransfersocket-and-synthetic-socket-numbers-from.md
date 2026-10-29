---
id: BL-4068
title: Remove FirstTransferSocket and synthetic socket numbers from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4036, BL-4037, BL-4038]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4068 — Remove FirstTransferSocket and synthetic socket numbers from ManagedMultiTransfer for CGR-034

## Goal

`ManagedMultiTransfer` no longer has `FirstTransferSocket` or synthetic socket numbers: a running transfer with no socket the handle knows is told no socket and is left out of `GetWaitSockets`.

## Context

Part 1 of 2 of BL-3990 (CGR-034). Drop `TellSocketCallback`'s `NeedsSocket` branch, `SocketNumberOf`, `nextTransferSocket`, `MultiEntry.Number` and `FirstTransferSocket` (and the `ResolverWakeupSocket` summary that cites it); `WaitSocketOf` answers `CurlSocket?` and nulls are skipped. Update the doc comments citing it in `ConnectOnlyConnection.cs` and `TransferSocketWait.cs`, and the assertion at `TransferSocketWaitTests.cs:106`. The 10 ms timer stays for BL-4069.

## Acceptance criteria

- [ ] `FirstTransferSocket`, `SocketNumberOf`, `nextTransferSocket` and `MultiEntry.Number` no longer exist in `Curl.Engine.UnitLibrary`.
- [ ] `GetWaitSockets` skips a running transfer with no known socket.
- [ ] The socket expectations in `ManagedMultiTransferTests.SocketAction.cs`, `.Counts.cs` and `TransferSocketWaitTests.cs` expect the real-socket answers (no synthetic "In null" sockets) and pass.
- [ ] `Cgr034Tests` still pin `C_20`, `C_74` and `C_75`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3990 (cost cap); BL-3990's Notes hold the full plan and decision.
- Continues the stashed work of BL-3990.

## Log

- 2026-10-29: Created.
