---
id: BL-4037
title: Give curl_multi_waitfds and the no-socket-callback multi handle each transfer's real socket instead of FirstTransferSocket numbers (CGR-034)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4037 — Give curl_multi_waitfds and the no-socket-callback multi handle each transfer's real socket instead of FirstTransferSocket numbers (CGR-034)

## Goal

`curl_multi_waitfds` (`ManagedMultiTransfer.GetWaitSockets`) names each running transfer's real socket (its operating system handle, or ADR-0955's engine number for a connection with none) rather than a `FirstTransferSocket` number, with or without a socket callback.

## Context

Prerequisite of BL-3990 (CGR-034), found by its 2026-10-28 run. `GetWaitSockets` uses `SocketNumberOf` (`FirstTransferSocket` + n) for every running transfer, and `SocketWaitFor` gives a transfer no `TransferSocketWait` when the handle has no socket callback, so without one the engine knows no real socket to report. This needs a record of the transfer's socket that does not gate its I/O (the `ConnectingOn` / `TakeSocket` bookkeeping without the gating) for transfers on a handle with no socket callback. Check that the upstream lib tests calling `curl_multi_waitfds` (test2405 and the like) stay green.

## Acceptance criteria

- [x] `GetWaitSockets` reports each running transfer's real socket number, with or without a socket callback, pinned by tests in `ManagedMultiTransferTests.Counts.cs`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan (ADR-0982): a ManagedEasyTransfer started on a handle with no socket callback gets a TransferSocketWait with gating stopped from the start, kept apart from the entry's gating SocketWait (MultiEntry.SocketRecord), so the socket-action dialer records its real socket (ConnectingOn / TakeSocket) without holding its I/O. GetWaitSockets, and a socket callback set later, name that socket; before the dial has one, and for transfers that are not ManagedEasyTransfer, the FirstTransferSocket number stays (the existing fake-transfer tests pin that).
- Only ManagedMultiTransfer.cs and the Counts tests changed; TransferSocketWait.cs and ManagedEasyTransfer.cs needed nothing (StopGating already makes every wait return at once).
- Tests: three in ManagedMultiTransferTests.Counts.cs (no callback with an OS socket, no callback with an engine-numbered connection, with a callback), each waiting on the dialer event it needs. Lib2405 tests run against FakeMultiTransfer and stay green; the full fast suite passed (46 test projects, Engine 2868 passed). Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members.
- CI on Linux and macOS: the tests use made-up socket numbers and no real socket, path or OS text, so they are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 34f617db 779790c0.
- 2026-10-28: Doing -> Done. curl_multi_waitfds names each running transfer's real socket, with or without a socket callback (ADR-0982)
