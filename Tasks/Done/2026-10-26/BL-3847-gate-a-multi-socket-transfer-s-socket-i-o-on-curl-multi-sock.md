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
completed: 2026-10-26
---
# BL-3847 — Gate a multi-socket transfer's socket I/O on curl_multi_socket_action and report what it waits for and its deadline

## Goal

Under `curl_multi_socket_action`, a running transfer touches its socket only after the application reports that socket ready (or the timer fires), and tells the multi handle what it waits for (writable while connecting or sending, readable while awaiting data) and its next deadline, so the multi handle can give libcurl's callback sequence and timeouts (prerequisite of BL-3563, CGR-034).

## Context

CGR-034 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Real libcurl, per the report's measurements: `add T0 kick S1=2(OUT) T-1 S1=1(IN) S1=4(REMOVE)`; with TIMEOUT_MS=600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1 results=28`; a refused connect `T0 S=2 T-1 S=4 results 7`. The managed engine runs each transfer as its own async task that reads and writes whenever data comes (ADR-0599), so with real sockets (BL-3846) it would consume data before the application's level-triggered poll saw it and the application would never be woken again. Hence ADR-0599's 10 ms polling timer (`RunningTransferCheckInterval`).

Build, in the engine, an `IConnection` wrapper used only for a transfer in a multi handle driven by socket actions: each read or write waits for a readiness signal the multi handle gives from `SocketActionAsync(socket, events)` (or perform / the timer), and publishes what it is waiting for (`CurlPoll.Out` before connect completes and while a write waits, `CurlPoll.In` while a read waits) plus the transfer's earliest deadline (connect timeout, `CURLOPT_TIMEOUT_MS`, low-speed, expect-100). Hook it in where `ManagedEasyTransfer` builds its connector. Data already buffered above the socket (TLS records, HTTP/2 frames) must not wait on a signal. Record the design in an ADR superseding ADR-0599's polling part.

## Acceptance criteria

- [x] A transfer in socket-action mode does not read from or write to its connection until the multi handle signals that connection ready, and exposes what it waits for (out/in) and its earliest deadline; unit tests on a fake connection and `FakeTimeProvider`-style clock show each.
- [x] Outside socket-action mode (easy perform, multi perform) transfers behave as before: the existing engine tests pass unchanged.
- [x] An ADR records the design, marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary shows no member outside a gate.

## Notes

- Filed by BL-3563's first pass (lane 5). BL-3563 then wires `ManagedMultiTransfer` to the real socket (BL-3846), the wait direction and the deadline from this task.
- Design (ADR-0821): `TransferSocketWait` holds the readiness signal (`Signal(CurlSocketSelect)`: In releases a read, Out a write, Error both; a signal with nothing waiting is kept for one wait, matching a level-triggered poll), `Waiting` (Out while connecting via `BeginConnect`/`EndConnect` or while a write waits, In while a read waits) and `EarliestDeadline` (connect timeout or libcurl's 300 s default until the connect ends, `CURLOPT_TIMEOUT_MS`, low-speed time from each wait's start, expect-100 between `BeginExpectContinueWait`/`EndExpectContinueWait`), with a `Changed` event for the multi handle. `SocketActionGatedConnection` gates reads and writes of a dialed TCP connection on it.
- Decision: the gate belongs beneath TLS and HTTP/2 framing (on the connection `ITcpDialer` gives), not on the pool's connection: `SslStream` exposes no count of buffered bytes, so above TLS a read answered from held records could not be told from one that must wait, and would hang on a signal that never comes. Beneath TLS those reads never reach the gate. The socket is what the multi handle signals, so a reused pooled connection keeps its socket's gate.
- Not done here, filed as BL-3859 (BL-3563 now depends on it): routing the transfer's dialer through the gate (`EngineComposition.Transports.cs` `DialersOf`, `ManagedCurlEngine.CreateTransports`, `ManagedEngineSharedState`, `ManagedEasyTransfer`, a new `SocketActionGatedTcpDialer` over `ITcpDialer`'s three dial methods) and feeding the expect-100 wait from the HTTP handler. Those files lie outside this task's touches and the dialer decorator is its own type, so `ManagedEasyTransfer.cs` was left unchanged here.
- Tests for both types are in `SocketActionGatedConnectionTests.cs`, the one test file the touches name (17 tests, on a settable `TimeProvider` and a recording fake connection).
- Checks: `dotnet build` 0 warnings, 0 errors; every fast test project passed (Curl.Engine.UnitTests 2527 passed, 6 skipped). Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members. The first run also listed `QueuedCallback.WaitUntilRun` (MultiCallbackQueue.cs:178-179, BL-3562's code, untouched here) as uncovered; it was covered on the next two runs, so its coverage depends on timing.
- Platform: the tests use no paths, OS errors or certificates, and every wait is released by an explicit signal or cancellation before it is awaited, so they do not depend on scheduling.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 8351b6aa a6abc742 1e378075 877bdc24.
- 2026-10-26: Doing -> Done. TransferSocketWait and SocketActionGatedConnection gate a socket-action transfer's reads and writes on the multi handle's readiness signal and publish OUT/IN and the earliest deadline (ADR-0821); routing is BL-3859
