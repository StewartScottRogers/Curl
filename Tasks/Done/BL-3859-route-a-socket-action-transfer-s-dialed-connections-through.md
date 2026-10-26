---
id: BL-3859
title: Route a socket-action transfer's dialed connections through SocketActionGatedConnection and feed its deadlines
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3847]
touches: [Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3859 — Route a socket-action transfer's dialed connections through SocketActionGatedConnection and feed its deadlines

## Goal

A transfer in a multi handle driven by `curl_multi_socket_action` dials its TCP connections through an `ITcpDialer` decorator that publishes `CurlPoll.Out` while the connect runs (`TransferSocketWait.BeginConnect`/`EndConnect`) and wraps each dialed connection, beneath TLS, in `SocketActionGatedConnection`, with `TransferSocketWait.Start` called at the perform's start and the HTTP handler's expect-100 wait feeding `BeginExpectContinueWait`/`EndExpectContinueWait`; outside socket-action mode nothing changes.

## Context

BL-3847 built `TransferSocketWait` and `SocketActionGatedConnection` (ADR-0821) with unit tests but did not route them: `ManagedCurlEngine.CreateTransports` passes `EngineComposition.CreateTransports` the dialer (`DialersOf` in `EngineComposition.Transports.cs`), so the gate must go there, carried on the transfer's `ManagedEngineSharedState` or a new optional parameter, and a pooled connection reused by a later transfer keeps the gate of the socket it wraps (the multi handle signals by socket). `ITcpDialer` has `DialAsync`, a bound-local-port variant and a Unix-socket one; the decorator wraps all. The expect-100 hook needs `Curl.Protocol.Http.UnitLibrary`'s wait (find it with `grep -n Expect100Timeout`); if that touches a file another live task holds, split it out. BL-3563 then has `ManagedMultiTransfer` create one `TransferSocketWait` per transfer, signal it from `SocketActionAsync`, and report `Waiting` and `EarliestDeadline` through the socket and timer callbacks, removing ADR-0599's 10 ms polling timer.

## Acceptance criteria

- [x] With socket-action mode on, a transfer over `ScriptedTcpDialer` reads and writes only after its `TransferSocketWait` is signalled, and `Waiting` is `Out` during the dial; a unit test shows each.
- [x] With socket-action mode off, the existing engine tests pass unchanged.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary shows no member outside a gate.

## Notes

- Added `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs` to touches: the perform's start, where `TransferSocketWait.Start` is called, is there; no task in Doing on origin/work/dark-factory names it.
- Plan (ADR-0822): `ManagedEasyTransfer.SocketWait` (internal, null by default; BL-3563 sets it) is started at each perform (`StartSocketWait`, a helper so `PerformFromOptionsOverCachesAsync` stays at complexity 10) and carried on `ManagedEngineSharedState.WithSocketWait`; `ManagedCurlEngine.CreateTransports` passes it to `EngineComposition.CreateTransports`'s new optional `socketWait`, which gives the `TcpConnector` `SocketActionGatedTcpDialer.Over(dialer, socketWait)` - the dialer itself when null, so outside socket-action mode nothing changes. The decorator calls `BeginConnect` before each dial (all five dial members), `EndConnect` when one connects, and wraps the connection in `SocketActionGatedConnection` beneath TLS; `FailureToOpenSocket` is forwarded.
- Default taken: a failed dial leaves the transfer connecting (libcurl stays `OUT` while it tries the next address), and the connect deadline ends at the TCP connect, not after TLS (the handshake's I/O is gated anyway). Recorded in ADR-0822.
- The expect-100 hook (`BeginExpectContinueWait`/`EndExpectContinueWait`) needs `Curl.Protocol.Http.UnitLibrary`, outside touches and not in the acceptance criteria: split out as BL-3864.
- Tests: `SocketActionGatedTcpDialerTests` - a perform over `ScriptedTcpDialer` (held mid-dial) shows `Waiting` is `Out` with a deadline during the dial, nothing written until the first signal, the transfer finishing `Ok` only as signals come; without a wait the connection is not gated; each dial member gates.
- Criterion 2: the whole fast suite passed with the change (0 failed). Criterion 3: build clean, fast tests green, Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10. The change is platform-neutral (no path, OS error or certificate; the test waits on `TransferSocketWait.Changed`, not on scheduling).

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: abd4b015 9f797c6a 877bdc24.
- 2026-10-26: Doing -> Done. A socket-action transfer dials through SocketActionGatedTcpDialer: OUT while it dials, its connections gated beneath TLS on its TransferSocketWait, Start called per perform; unchanged without one
