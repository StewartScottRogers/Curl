---
id: BL-4068
title: Remove FirstTransferSocket and synthetic socket numbers from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4036, BL-4037, BL-4038, BL-4070]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.MultiEntry.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs]
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
- 2026-10-29 run (lane 2): added `ManagedMultiTransfer.MultiEntry.cs` (holds `MultiEntry.Number`), `ManagedMultiTransferTests.Timers.cs` (line 267) and `Cgr148Tests.cs` (line 17) to `touches`: each names `FirstTransferSocket` or `Number`, so the removal does not compile without them; no task in Doing on origin/work/dark-factory names them (checked). The stash holds the whole production change (`NeedsSocket`, `SocketNumberOf`, `nextTransferSocket`, `MultiEntry.Number`, `FirstTransferSocket` gone; `WaitSocketOf` answers `CurlSocket?`, nulls skipped, the resolver wakeup only after a real socket; doc comments in `ConnectOnlyConnection.cs`, `TransferSocketWait.cs`, `WaitSockets.cs` and the class summary, which keeps the 10 ms interval for BL-4069 - the earlier stash's "timer is 0 ... ADR-0989" text was wrong, ADR-0989 is an OpenSSL ADR) and the test updates in `SocketAction.cs` (timer-only sequences; `Remove`, `Cgr148`'s socket-callback abort over `ReadableWaitingTransfer`, a wrapped transfer holding `WrappedSocket`; `WrappingEasyTransfer` and `WrappedSocket` made internal), `Counts.cs` (no-socket and real-socket `GetWaitSockets` tests over `SocketHoldingTransfer`), `Timers.cs`, `Cgr148Tests.cs` and `TransferSocketWaitTests.cs`. Build clean; of the 141 ManagedMultiTransfer/Cgr148/Cgr034/TransferSocketWait tests 8 fail: the three tests BL-4070 covers (Limits.cs and ManagedMultiTransferTests.cs, outside `touches`) and `Counts.cs`'s `EnableNotification_AbortedByCallback_SendsEasyDoneForEachTransferItEnds`, which aborts through the socket callback a fake transfer is no longer told of: drive it over `ReadableWaitingTransfer` on the next claim. Still to do after that: the ADR (decided by Claude under Stewart's delegation: a transfer with no socket the handle knows is told none and left out of waitfds; real libcurl has no such transfer, so nothing to measure), the Curl.Engine.UnitLibrary CLAUDE.md BL-2426/BL-2427 paragraphs (BL-4069 also touches that file), one Measure-CodeQuality.ps1 run for Curl.Engine.UnitLibrary and the full fast tests.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c707c13f aaefc491 da38b818 18d43fff 779790c0.
- 2026-10-29: Doing -> Backlog. Needs BL-4070 (Limits.cs and ManagedMultiTransferTests.cs tests over real sockets) before the synthetic socket numbers can go
- 2026-10-29: Backlog -> Deferred. Split for cost cap into BL-4072 (code and tests) and BL-4073 (ADR, CLAUDE.md, quality run)
