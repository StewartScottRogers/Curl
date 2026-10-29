---
id: BL-4038
title: Give a non-managed IEasyTransfer (StoreBoundEasyTransfer, test fakes) on a socket-action multi handle a socket wait, and bring a transfer that computes past SocketWaitSettleLimit back without a timer (CGR-034)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4037]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs, Curl.Api/Stores/StoreBoundEasyTransfer.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Api.UnitTests/Stores/StoreBoundEasyTransferTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4038 — Give a non-managed IEasyTransfer (StoreBoundEasyTransfer, test fakes) on a socket-action multi handle a socket wait, and bring a transfer that computes past SocketWaitSettleLimit back without a timer (CGR-034)

## Goal

Every transfer on a socket-action multi handle has a socket the application is told of, including an `IEasyTransfer` that is not a `ManagedEasyTransfer`, and a transfer still computing (waiting on nothing) after `SocketWaitSettleLimit` is brought back without the 10 ms `RunningTransferCheckInterval` timer.

## Context

Prerequisite of BL-3990 (CGR-034), found by its 2026-10-28 run. `ManagedMultiTransfer.Start` sets a `TransferSocketWait` only on a `ManagedEasyTransfer`, so Curl.Api's `StoreBoundEasyTransfer` (which wraps one) and every test fake get a synthetic `FirstTransferSocket` number from `TellSocketCallback`'s `NeedsSocket` branch and rely on the 10 ms timer. A transfer whose `Waiting` is `None` when `SettleSocketWaitingTransfers` reaches its limit also relies on that timer. Decide how a wrapped or fake transfer exposes its socket wait (for example a member on `IEasyTransfer` that `StoreBoundEasyTransfer` forwards) and how a runnable transfer is brought back (for example a 0 ms timer, as libcurl's `Curl_expire(data, 0)` for a transfer that can run on); record both in an ADR. Runs after BL-4037, which changes the same file.

## Acceptance criteria

- [x] A non-managed `IEasyTransfer` added to a handle with a socket callback is told a socket from its own socket wait, pinned by tests in `ManagedMultiTransferTests.SocketAction.cs`.
- [x] A transfer still computing after `SocketWaitSettleLimit` is brought back without `RunningTransferCheckInterval`, pinned by a test in `ManagedMultiTransferTests.Timers.cs`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Decided (ADR-0983): `IEasyTransfer.PerformingTransfer` (default: the handle itself) names the handle whose perform does the transfer; `StoreBoundEasyTransfer` forwards it, and `ManagedMultiTransfer.Start` gives the socket wait and multi callbacks to the `PerformingTransfer` when it is a `ManagedEasyTransfer`. The seam names the handle, not the wait, because `TransferSocketWait` is internal to the engine. A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number (it has no socket to tell).
- Decided (ADR-0983): while a running transfer on a socket wait waits on nothing after the settle, the timer is 0 (libcurl's `Curl_expire(data, 0)`), also for one whose perform ended and waits to be reported; the 10 ms interval stays only for transfers with no socket wait.
- Added `Curl.Api.UnitTests/Stores/StoreBoundEasyTransferTests.cs` to touches: Curl.Api's 100% coverage gate needs the forwarder tested; no task in Doing on origin/work/dark-factory names it.
- `RunningTimerMilliseconds` went past the complexity gate (cached method-group delegates count); split into `CheckIntervalMilliseconds`. Measure-CodeQuality for Curl.Engine.UnitLibrary, Curl.Api, Curl.Api.UnitLibrary: 0 failing members. Fast tests green on Windows; the tests use no path, OS error or certificate and wait on the transfer's own events, so they are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 46b14221 05bdc202 779790c0.
- 2026-10-28: Doing -> Done. A wrapped engine transfer (StoreBoundEasyTransfer) on a socket-action handle is told its real socket via IEasyTransfer.PerformingTransfer, and a computing socket-wait transfer asks for timer 0 (ADR-0983)
