---
id: BL-3849
title: Route a socket-action transfer's dialed connections through SocketActionGatedConnection and feed its deadlines
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3847]
touches: [Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/SocketActionGatedTcpDialerTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3849 — Route a socket-action transfer's dialed connections through SocketActionGatedConnection and feed its deadlines

## Goal

A transfer in a multi handle driven by `curl_multi_socket_action` dials its TCP connections through an `ITcpDialer` decorator that publishes `CurlPoll.Out` while the connect runs (`TransferSocketWait.BeginConnect`/`EndConnect`) and wraps each dialed connection, beneath TLS, in `SocketActionGatedConnection`, with `TransferSocketWait.Start` called at the perform's start and the HTTP handler's expect-100 wait feeding `BeginExpectContinueWait`/`EndExpectContinueWait`; outside socket-action mode nothing changes.

## Context

BL-3847 built `TransferSocketWait` and `SocketActionGatedConnection` (ADR-0821) with unit tests but did not route them: `ManagedCurlEngine.CreateTransports` passes `EngineComposition.CreateTransports` the dialer (`DialersOf` in `EngineComposition.Transports.cs`), so the gate must go there, carried on the transfer's `ManagedEngineSharedState` or a new optional parameter, and a pooled connection reused by a later transfer keeps the gate of the socket it wraps (the multi handle signals by socket). `ITcpDialer` has `DialAsync`, a bound-local-port variant and a Unix-socket one; the decorator wraps all. The expect-100 hook needs `Curl.Protocol.Http.UnitLibrary`'s wait (find it with `grep -n Expect100Timeout`); if that touches a file another live task holds, split it out. BL-3563 then has `ManagedMultiTransfer` create one `TransferSocketWait` per transfer, signal it from `SocketActionAsync`, and report `Waiting` and `EarliestDeadline` through the socket and timer callbacks, removing ADR-0599's 10 ms polling timer.

## Acceptance criteria

- [ ] With socket-action mode on, a transfer over `ScriptedTcpDialer` reads and writes only after its `TransferSocketWait` is signalled, and `Waiting` is `Out` during the dial; a unit test shows each.
- [ ] With socket-action mode off, the existing engine tests pass unchanged.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary shows no member outside a gate.

## Notes

## Log

- 2026-10-26: Created.
