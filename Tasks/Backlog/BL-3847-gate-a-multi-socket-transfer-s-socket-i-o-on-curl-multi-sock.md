---
id: BL-3847
title: Gate a multi-socket transfer's socket I/O on curl_multi_socket_action and report what it waits for and its deadline
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846]
touches: [Curl.Engine.UnitLibrary/SocketActionGatedConnection.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/SocketActionGatedConnectionTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3847 — Gate a multi-socket transfer's socket I/O on curl_multi_socket_action and report what it waits for and its deadline

## Goal

Under `curl_multi_socket_action`, a running transfer touches its socket only after the application reports that socket ready (or the timer fires), and tells the multi handle what it waits for (writable while connecting or sending, readable while awaiting data) and its next deadline, so the multi handle can give libcurl's callback sequence and timeouts (prerequisite of BL-3563, CGR-034).

## Context

CGR-034 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Real libcurl, per the report's measurements: `add T0 kick S1=2(OUT) T-1 S1=1(IN) S1=4(REMOVE)`; with TIMEOUT_MS=600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1 results=28`; a refused connect `T0 S=2 T-1 S=4 results 7`. The managed engine runs each transfer as its own async task that reads and writes whenever data comes (ADR-0599), so with real sockets (BL-3846) it would consume data before the application's level-triggered poll saw it and the application would never be woken again. Hence ADR-0599's 10 ms polling timer (`RunningTransferCheckInterval`).

Build, in the engine, an `IConnection` wrapper used only for a transfer in a multi handle driven by socket actions: each read or write waits for a readiness signal the multi handle gives from `SocketActionAsync(socket, events)` (or perform / the timer), and publishes what it is waiting for (`CurlPoll.Out` before connect completes and while a write waits, `CurlPoll.In` while a read waits) plus the transfer's earliest deadline (connect timeout, `CURLOPT_TIMEOUT_MS`, low-speed, expect-100). Hook it in where `ManagedEasyTransfer` builds its connector. Data already buffered above the socket (TLS records, HTTP/2 frames) must not wait on a signal. Record the design in an ADR superseding ADR-0599's polling part.

## Acceptance criteria

- [ ] A transfer in socket-action mode does not read from or write to its connection until the multi handle signals that connection ready, and exposes what it waits for (out/in) and its earliest deadline; unit tests on a fake connection and `FakeTimeProvider`-style clock show each.
- [ ] Outside socket-action mode (easy perform, multi perform) transfers behave as before: the existing engine tests pass unchanged.
- [ ] An ADR records the design, marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary shows no member outside a gate.

## Notes

- Filed by BL-3563's first pass (lane 5). BL-3563 then wires `ManagedMultiTransfer` to the real socket (BL-3846), the wait direction and the deadline from this task.

## Log

- 2026-10-26: Created.
