---
id: BL-4015
title: Land the stashed TransferSocketWait, gated dialer and ConnectOnlyConnection socket-action pieces
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890]
touches: [Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs, Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4015 — Land the stashed TransferSocketWait, gated dialer and ConnectOnlyConnection socket-action pieces

## Goal

`TransferSocketWait`, `SocketActionGatedTcpDialer` and `ConnectOnlyConnection` carry the socket-action pieces of the stashed work (ADR-0828, ADR-0955 `TakeSocket` and `SignalOn`), additive and unused by `ManagedMultiTransfer` until BL-4016, so the fast suite stays green on its own.

## Context

Split from BL-4008 (cost cap), itself split from BL-3989, BL-3976 and BL-3563 (CGR-034 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; curl 8.22.0). Read the Notes of BL-4008 first.

## Acceptance criteria

- [ ] The stash's changes to `TransferSocketWait.cs`, `SocketActionGatedTcpDialer.cs` and `ConnectOnlyConnection.cs` are applied, keeping ADR-0955's `FirstEngineSocket` and dropping `DefaultConnectTimeout` in `TransferSocketWait.cs`.
- [ ] `Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs` and `SocketActionGatedTcpDialerTests.cs` cover the new members, including the no-OS-socket test named `..._TellsItsEngineSocketOutThenInThenRemove`, and fail before the change and pass after it (ADR-0482).
- [ ] `ManagedMultiTransfer.cs` is unchanged and upstream test530 and test582 still pass.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Engine.UnitTests`.

## Notes

- Split from BL-4008 (cost cap). Continues the stashed work of BL-4008.
- Take only the stash's hunks for the files in `touches`; the `ManagedMultiTransfer.cs`, `Cgr034Tests.cs` and `ManagedMultiTransferTests.SocketAction.cs` hunks belong to BL-4016.
- The stash's ADR (renumbered to ADR-0969 by the last run; check the number is still free) is written by BL-4016, which wires it.

## Log

- 2026-10-28: Created.
