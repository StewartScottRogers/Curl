---
id: BL-2730
title: Retire a pooled connection older than CURLOPT_MAXLIFETIME_CONN so upstream case 1542 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2730 — Retire a pooled connection older than CURLOPT_MAXLIFETIME_CONN so upstream case 1542 passes

## Goal

A transfer that would reuse a pooled connection made longer ago than `CURLOPT_MAXLIFETIME_CONN` (`EasyNetworkOptions.MaxConnectionLifetime`) closes it with `shutting down connection #N` and opens a new one instead, as upstream's `conncache` does, so upstream case 1542 passes.

## Context

Found by BL-2650. Case 1542 (`lib1542`, `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs`) now runs and fails: after three reuses it sets `CURLOPT_MAXLIFETIME_CONN` to 1 second, waits, and the fourth transfer must not reuse connection #0. Curl reuses it. `MaxConnectionLifetime` is stored in `Curl.Api.UnitLibrary/Options/Network/EasyNetworkOptions.cs` but nothing reads it; the pool's eviction and `shutting down connection #N` lines are in `Curl.Networking.UnitLibrary/PoolingConnector.cs`. Use the injected `TimeProvider` for the connection's age (the harness's `WaitSkippingTimeProvider` skips the wait).

`CURL_UPSTREAM_CASE=1542` differs at line 4 of `stderr1542` (after the strip):

```
  2: "== Info: Connection #0 to host 127.0.0.1:8990 left intact\r\n"
  3: "== Info: Connection #0 to host 127.0.0.1:8990 left intact\r\n"
- 4: "== Info: shutting down connection #0\r\n"
- 5: "== Info: Connection #1 to host 127.0.0.1:8990 left intact\r\n"
+ 4: "== Info: Connection #0 to host 127.0.0.1:8990 left intact\r\n"
```

## Acceptance criteria

- [x] A pooled connection older than the transfer's `MaxConnectionLifetime` is not reused: it is closed with `shutting down connection #N`, with a focused unit test in `Curl.Networking.UnitTests` for one just under and one past the limit.
- [x] `CURL_UPSTREAM_CASE=1542` passes and 1542 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] Every case on `PassingUpstreamCases.txt` still passes; `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 4, first pass, no code written): nothing reads `EasyNetworkOptions.MaxConnectionLifetime`, and
  the only place a transfer's `EasyNetworkOptions` reach the pool is
  `Curl.Engine.UnitLibrary/EngineComposition.cs` `CreatePoolingConnector` (called from `CreateTransports` with the
  perform's options, over the run's shared `ConnectionCache`). So `Curl.Engine.UnitLibrary` was added to `touches`.
  Plan, complete apart from that: (1) `PoolEntry` gains a creation timestamp (`_cache.TimeProvider.GetTimestamp()`
  in `OpenAsync`); (2) `PoolingConnector` gains an `init` property `MaximumConnectionLifetime` (default 24 h, zero
  for no limit, as `CURLOPT_MAXLIFETIME_CONN`); (3) `TakeIdleAsync` treats a match whose age exceeds it like a dead
  one: `shutting down connection #N`, close, try the next; (4) `CreatePoolingConnector` sets it from
  `network.MaxConnectionLifetime` on both constructors' connectors; (5) unit tests in `PoolingConnectorTests` for
  just under and just past the limit with `ManualTimeProvider`; (6) run `CURL_UPSTREAM_CASE=1542`, add 1542 to the
  ratchet. Libcurl's `SetMaxConnectionLifetime` setter already exists. No other prerequisite found.
- 2026-10-19 (lane 2): done as planned. `PoolEntry.OpenedAt`; `PoolingConnector.MaximumConnectionLifetime`
  (init, 24 h default, zero for no limit, kept by `Over`); `TakeIdleAsync` retires a match older than it (strictly
  greater, as upstream's `conn_maxage` compares) with only `shutting down connection #N`, the line test1542 checks;
  `CreatePoolingConnector` sets it on both connectors. Four tests in `PoolingConnectorTests` (1000 ms reused,
  1001 ms retired, zero no limit, `Over`). `CURL_UPSTREAM_CASE=1542` passes and is on the ratchet; fast tests green;
  Measure-CodeQuality: Networking and Engine 100/100 line/branch, 0 failing members. The ADR-0694 touches
  narrowing was not published, as this run may not push.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: af82bdd2 dd52269f 03ca90eb.
- 2026-10-18: Doing -> Backlog. touches Curl.Engine.UnitLibrary (CreatePoolingConnector must pass MaxConnectionLifetime to the pool), which BL-2735 in Doing also touches
- 2026-10-19: Backlog -> Doing. Criteria at claim: af82bdd2 dd52269f 03ca90eb.
- 2026-10-19: Doing -> Done. A pooled connection older than CURLOPT_MAXLIFETIME_CONN is shut down and replaced; upstream case 1542 passes and is on the ratchet
