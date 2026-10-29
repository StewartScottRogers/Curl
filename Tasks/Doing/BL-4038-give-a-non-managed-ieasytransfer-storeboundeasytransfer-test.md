---
id: BL-4038
title: Give a non-managed IEasyTransfer (StoreBoundEasyTransfer, test fakes) on a socket-action multi handle a socket wait, and bring a transfer that computes past SocketWaitSettleLimit back without a timer (CGR-034)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4037]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs, Curl.Api/Stores/StoreBoundEasyTransfer.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4038 — Give a non-managed IEasyTransfer (StoreBoundEasyTransfer, test fakes) on a socket-action multi handle a socket wait, and bring a transfer that computes past SocketWaitSettleLimit back without a timer (CGR-034)

## Goal

Every transfer on a socket-action multi handle has a socket the application is told of, including an `IEasyTransfer` that is not a `ManagedEasyTransfer`, and a transfer still computing (waiting on nothing) after `SocketWaitSettleLimit` is brought back without the 10 ms `RunningTransferCheckInterval` timer.

## Context

Prerequisite of BL-3990 (CGR-034), found by its 2026-10-28 run. `ManagedMultiTransfer.Start` sets a `TransferSocketWait` only on a `ManagedEasyTransfer`, so Curl.Api's `StoreBoundEasyTransfer` (which wraps one) and every test fake get a synthetic `FirstTransferSocket` number from `TellSocketCallback`'s `NeedsSocket` branch and rely on the 10 ms timer. A transfer whose `Waiting` is `None` when `SettleSocketWaitingTransfers` reaches its limit also relies on that timer. Decide how a wrapped or fake transfer exposes its socket wait (for example a member on `IEasyTransfer` that `StoreBoundEasyTransfer` forwards) and how a runnable transfer is brought back (for example a 0 ms timer, as libcurl's `Curl_expire(data, 0)` for a transfer that can run on); record both in an ADR. Runs after BL-4037, which changes the same file.

## Acceptance criteria

- [ ] A non-managed `IEasyTransfer` added to a handle with a socket callback is told a socket from its own socket wait, pinned by tests in `ManagedMultiTransferTests.SocketAction.cs`.
- [ ] A transfer still computing after `SocketWaitSettleLimit` is brought back without `RunningTransferCheckInterval`, pinned by a test in `ManagedMultiTransferTests.Timers.cs`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 46b14221 05bdc202 779790c0.
