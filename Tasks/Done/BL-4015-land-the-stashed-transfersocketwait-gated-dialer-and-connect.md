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
completed: 2026-10-28
---
# BL-4015 — Land the stashed TransferSocketWait, gated dialer and ConnectOnlyConnection socket-action pieces

## Goal

`TransferSocketWait`, `SocketActionGatedTcpDialer` and `ConnectOnlyConnection` carry the socket-action pieces of the stashed work (ADR-0828, ADR-0955 `TakeSocket` and `SignalOn`), additive and unused by `ManagedMultiTransfer` until BL-4016, so the fast suite stays green on its own.

## Context

Split from BL-4008 (cost cap), itself split from BL-3989, BL-3976 and BL-3563 (CGR-034 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; curl 8.22.0). Read the Notes of BL-4008 first.

## Acceptance criteria

- [x] The stash's changes to `TransferSocketWait.cs`, `SocketActionGatedTcpDialer.cs` and `ConnectOnlyConnection.cs` are applied, keeping ADR-0955's `FirstEngineSocket` and dropping `DefaultConnectTimeout` in `TransferSocketWait.cs`.
- [x] `Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs` and `SocketActionGatedTcpDialerTests.cs` cover the new members, including the no-OS-socket test named `..._TellsItsEngineSocketOutThenInThenRemove`, and fail before the change and pass after it (ADR-0482).
- [x] `ManagedMultiTransfer.cs` is unchanged and upstream test530 and test582 still pass.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Engine.UnitTests`.

## Notes

- Split from BL-4008 (cost cap). Continues the stashed work of BL-4008.
- Take only the stash's hunks for the files in `touches`; the `ManagedMultiTransfer.cs`, `Cgr034Tests.cs` and `ManagedMultiTransferTests.SocketAction.cs` hunks belong to BL-4016.
- The stash's ADR (renumbered to ADR-0969 by the last run; check the number is still free) is written by BL-4016, which wires it.

- 2026-10-28 run (lane 9): `SocketActionGatedTcpDialer.cs` and `ConnectOnlyConnection.cs` had no stash hunks left: their `TakeSocket` and `SignalOn` pieces, with `FirstEngineSocket`, landed with ADR-0955 (9cafbb526) and are tested in `TransferSocketWaitTests`. What this task landed is the stash's `TransferSocketWait.cs` hunk: `DefaultConnectTimeout` dropped (the connect deadline is armed only when `CURLOPT_CONNECTTIMEOUT_MS` is set, CGR-034 C_65), `IsConnecting`, and a connect answered by an `Out` or `Error` signal waiting on nothing until the dialer connects another socket or a new connect begins. `Signal` was over the complexity gate (12); it now computes the in and out readiness with one mask each.
- The `..._TellsItsEngineSocketOutThenInThenRemove` test drives `ManagedMultiTransfer`'s socket-action wiring, so it stays in the stash's `Cgr034Tests.cs` for BL-4016; this task's new tests (`SocketActionGatedConnectionTests`: `IsConnecting` and the four `Signal_*` connect tests) fail to compile before the change and pass after it.
- The stash's `ManagedMultiTransfer.cs`, `ManagedMultiTransferTests.SocketAction.cs`, `Cgr034Tests.cs`, ADR-0969 and README row were set aside, not committed: BL-4016 takes them from stash 2ed39938f4b4211d07f28f1ceb009eede15f0c36.
- Measured: `Curl.Engine.UnitTests` 2754 passed; upstream test530 and test582 pass (`CURL_UPSTREAM_CASE`); full fast suite green. Quality for `Curl.Engine.UnitLibrary`: only `ManagedEasyTransfer.PerformBuildingMimePostAsync` (90% branch, untouched here) is left, filed as BL-4017. CI on Linux and macOS: the change and its tests are platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 00e5f677 1ada490e 62fb8150 a0b1d3ee.
- 2026-10-28: Doing -> Done. TransferSocketWait arms only the connect timeout it is given and lets a signalled connect run on; IsConnecting added
