---
id: BL-334
title: Compose one pooling connector per command line in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-335, BL-215]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-334 — Compose one pooling connector per command line in Curl.Console

## Goal

`CurlComposition.CreateTransports` wraps its `TcpConnector` in one `PoolingConnector` per command line, hands it to every handler, and disposes it when the run ends, so a second URL to the same key reuses the first URL's connection.

## Context

- ADR-0050, section "A pooling connector owns the pool, one per command line". If the ADR and this task disagree, the ADR wins.
- `PoolingConnector` is built by BL-215 in `Curl.Networking.UnitLibrary`; the contract comes from BL-335.
- Disposing the pool at the end of the run writes nothing (curl's `-v` shows nothing after the last `left intact`).
- `--next`, `-Z`/`--parallel` and `--no-keepalive` are not part of this task.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` (`CurlTransportsTests` or `CurlCompositionTests`) pins that every handler the composition builds receives the same `PoolingConnector` instance.
- [x] A test pins that the pool is disposed when the run ends, including when a transfer fails.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the ADR-0050
  section already is the plan, and the change is one composition wiring plus disposal.
- `CurlTransports` gains a ninth member, `PoolingConnector`, built in `CreateTransports` over
  the `TcpConnector` on the same `TimeProvider`. `CreateDispatcher` and
  `CreateTransferDispatch(CurlTransports, ...)` hand it to every handler in place of the
  `TcpConnector`.
- Disposal: `TransferDispatch` takes an optional `IAsyncDisposable connectionPool` (exposed as
  `ConnectionPool`) and is itself `IAsyncDisposable`; `CurlCommandRunner.TransferAllAsync`
  disposes the dispatch in its `finally`, before the transfer-event output. Typed
  `IAsyncDisposable` rather than `PoolingConnector` so runner tests can count disposals with a
  hand-rolled fake (`PoolingConnector` is sealed).
- The test-only `CreateRunner(..., IConnector, ...)` overload still passes its injected
  connector straight to the handlers, unpooled, so existing multi-URL tests keep one connect
  per URL.
- Tests: `CurlCompositionTests` (every connecting handler - dict, gopher, telnet, mqtt, http,
  forwarded ftp - holds exactly the transports' `PoolingConnector`; the pool wraps the TCP
  connector on its clock; two URLs to one host connect once), `CurlCommandRunnerConnectionPoolTests`
  (disposed once after a success, a failed connect, and a malformed glob; nothing written).
- `Measure-CodeQuality.ps1 -Library Curl.Console` without Integration reports 99.48% only because
  `DiskWriteOutFileOpener.TryOpen` is covered by Integration tests by design (BL-280), unchanged
  here; with `-IncludeIntegration` it reports 100% line, 100% branch, 0 failing.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console composes one PoolingConnector per command line; every TCP handler shares it and the run disposes it
