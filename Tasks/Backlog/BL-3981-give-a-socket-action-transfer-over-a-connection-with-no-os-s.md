---
id: BL-3981
title: Give a socket-action transfer over a connection with no OS socket a socket number the application is told of and woken on (CGR-034)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs, Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs, Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3981 — Give a socket-action transfer over a connection with no OS socket a socket number the application is told of and woken on (CGR-034)

## Goal

A transfer driven by `curl_multi_socket_action` whose connection has no operating system socket (`IConnection.SocketHandle` null: the conformance harness's in-memory dialing) still has a socket the socket callback is told of and an action on it wakes, so `ManagedMultiTransfer` no longer needs its `FirstTransferSocket` numbers or its 10 ms `RunningTransferCheckInterval` timer (BL-3976).

## Context

Prerequisite of BL-3976 (CGR-034, ADR-0948). BL-3976's stash (kept by the shift; its Notes name it) wires `ManagedMultiTransfer` to `TransferSocketWait`: a transfer over a real socket is told `CURL_POLL_OUT` then `CURL_POLL_IN` and the timer gets its deadline. A connection with no OS socket gets no socket callback and is woken only by `CURL_SOCKET_TIMEOUT` actions that the 10 ms timer brings, which upstream cases run in-process by `Lib530Run` and `Lib582Run` (in-memory servers) rely on; removing the timer without this hangs them. Decide (ADR) where the number comes from - for instance `TransferSocketWait` handing out an engine socket number from its own range, as `ConnectOnlyConnection.FirstSocket` does, which a wait on it answers from the connection - and how the libtest runs poll it.

## Acceptance criteria

- [ ] `TransferSocketWait.Socket` is set for a connection whose `SocketHandle` is null, to a number distinct from every operating system socket the engine hands out, and an action naming it signals the wait.
- [ ] Upstream cases run by `Lib530Run` and `Lib582Run` drive such transfers through that socket and pass without a timeout action every 10 ms.
- [ ] Tests in `Curl.Engine.UnitTests/TransferSocketWaitTests.cs` pin the number and the wake-up.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3976's run of 2026-10-28, which found its criteria 1 and 2 cannot hold while in-memory connections have no socket.

## Log

- 2026-10-28: Created.
