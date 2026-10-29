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
completed:
---
# BL-4037 — Give curl_multi_waitfds and the no-socket-callback multi handle each transfer's real socket instead of FirstTransferSocket numbers (CGR-034)

## Goal

`curl_multi_waitfds` (`ManagedMultiTransfer.GetWaitSockets`) names each running transfer's real socket (its operating system handle, or ADR-0955's engine number for a connection with none) rather than a `FirstTransferSocket` number, with or without a socket callback.

## Context

Prerequisite of BL-3990 (CGR-034), found by its 2026-10-28 run. `GetWaitSockets` uses `SocketNumberOf` (`FirstTransferSocket` + n) for every running transfer, and `SocketWaitFor` gives a transfer no `TransferSocketWait` when the handle has no socket callback, so without one the engine knows no real socket to report. This needs a record of the transfer's socket that does not gate its I/O (the `ConnectingOn` / `TakeSocket` bookkeeping without the gating) for transfers on a handle with no socket callback. Check that the upstream lib tests calling `curl_multi_waitfds` (test2405 and the like) stay green.

## Acceptance criteria

- [ ] `GetWaitSockets` reports each running transfer's real socket number, with or without a socket callback, pinned by tests in `ManagedMultiTransferTests.Counts.cs`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
