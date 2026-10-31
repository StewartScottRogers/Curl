---
id: BL-3981
title: Give a socket-action transfer over a connection with no OS socket a socket number the application is told of and woken on (CGR-034)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4015, BL-4016, BL-4007, BL-4072, BL-4073, BL-4069]
touches: [Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/TransferSocketWaitTests.cs, Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs, Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-29
---
# BL-3981 — Give a socket-action transfer over a connection with no OS socket a socket number the application is told of and woken on (CGR-034)

## Goal

A transfer driven by `curl_multi_socket_action` whose connection has no operating system socket (`IConnection.SocketHandle` null: the conformance harness's in-memory dialing) still has a socket the socket callback is told of and an action on it wakes, so `ManagedMultiTransfer` no longer needs its `FirstTransferSocket` numbers or its 10 ms `RunningTransferCheckInterval` timer (BL-3976).

## Context

Prerequisite of BL-3976 (CGR-034, ADR-0948). BL-3976's stash (kept by the shift; its Notes name it) wires `ManagedMultiTransfer` to `TransferSocketWait`: a transfer over a real socket is told `CURL_POLL_OUT` then `CURL_POLL_IN` and the timer gets its deadline. A connection with no OS socket gets no socket callback and is woken only by `CURL_SOCKET_TIMEOUT` actions that the 10 ms timer brings, which upstream cases run in-process by `Lib530Run` and `Lib582Run` (in-memory servers) rely on; removing the timer without this hangs them. Decide (ADR) where the number comes from - for instance `TransferSocketWait` handing out an engine socket number from its own range, as `ConnectOnlyConnection.FirstSocket` does, which a wait on it answers from the connection - and how the libtest runs poll it.

## Acceptance criteria

- [x] `TransferSocketWait.Socket` is set for a connection whose `SocketHandle` is null, to a number distinct from every operating system socket the engine hands out, and an action naming it signals the wait.
- [x] Upstream cases run by `Lib530Run` and `Lib582Run` drive such transfers through that socket and pass without a timeout action every 10 ms.
- [x] Tests in `Curl.Engine.UnitTests/TransferSocketWaitTests.cs` pin the number and the wake-up.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3976's run of 2026-10-28, which found its criteria 1 and 2 cannot hold while in-memory connections have no socket.

- 2026-10-28 run (lane 4): `TransferSocketWait.TakeSocket(nint?)` sets `Socket` once a dial gives a connection: its OS handle, or with none the engine's next number from `FirstEngineSocket` (`0x50000000`, apart from ConnectOnly's `0x60000000` and `FirstTransferSocket`'s `0x40000000`); `SignalOn(socket, events)` signals the wait when an action names its socket (ADR-0955). `SocketActionGatedTcpDialer` calls `TakeSocket` after each dial (added to `touches`; no task in Doing on origin/work/dark-factory names it). Seven tests in `TransferSocketWaitTests`. Build clean, fast tests green, Measure-CodeQuality for Curl.Engine.UnitLibrary 0 failing members. `Lib530Run` and `Lib582Run` act on every socket in their sets each turn (ADR-0663), so they need no change.
- Criterion 2 cannot be checked here: `ManagedMultiTransfer` in this tree does not use `TransferSocketWait` at all - the wiring and the removal of the 10 ms timer are BL-3976's stashed work, and BL-3976 listed this task in its `depends-on`, a cycle. Broken by committing this run's finished code (criteria 1, 3, 4 met, so BL-3976 finds it in the tree, not in a stash only this task's claim restores), removing BL-3981 from BL-3976's `depends-on`, and requeueing this task on BL-3976. Once BL-3976 is Done, the next claim only runs the upstream cases of `Lib530Run` and `Lib582Run` to tick criterion 2.
- 2026-10-28 run (lane 3): BL-4016 is Done (socket actions wired through `ManagedMultiTransfer`; test530 and test582 pass), but `RunningTransferCheckInterval` (10 ms) is still in `ManagedMultiTransfer.cs:106`; removing it is BL-3990, in Doing on lane 6 (BL-3976 is Deferred, its work split to BL-3990). Added BL-3990 to `depends-on` and requeued before any edit. Once BL-3990 is Done, the next claim runs the upstream cases of `Lib530Run` and `Lib582Run` (`CURL_UPSTREAM_CASE` 530, 582) to tick criterion 2.

- 2026-10-29 run (lane 1): BL-4069 and BL-4072 (Done) removed `RunningTransferCheckInterval` and `FirstTransferSocket` from `ManagedMultiTransfer`; only `SocketPollInterval` (curl_multi_wait extra sockets, not socket action) is left. `CURL_UPSTREAM_CASE` 530 and 582 through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` both pass, so criterion 2 holds with no code change. Build clean, fast tests green; platform-neutral (no code changed).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 3d275639 25a722b8 a00c97c3 779790c0.
- 2026-10-28: Doing -> Backlog. Criteria 1, 3, 4 met and committed; criterion 2 needs BL-3976's ManagedMultiTransfer wiring (cycle broken: BL-3976 no longer waits on BL-3981)
- 2026-10-28: Backlog -> Doing. Criteria at claim: 3d275639 25a722b8 a00c97c3 779790c0.
- 2026-10-28: Doing -> Backlog. Criterion 2 needs the 10 ms timer removed from ManagedMultiTransfer: BL-3990 (Doing on lane 6)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3d275639 25a722b8 a00c97c3 779790c0.
- 2026-10-29: Doing -> Done. Socket-action transfers over in-memory connections get an engine socket number; test530 and test582 pass without the 10 ms timer
