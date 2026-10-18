---
id: BL-2730
title: Retire a pooled connection older than CURLOPT_MAXLIFETIME_CONN so upstream case 1542 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
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

- [ ] A pooled connection older than the transfer's `MaxConnectionLifetime` is not reused: it is closed with `shutting down connection #N`, with a focused unit test in `Curl.Networking.UnitTests` for one just under and one past the limit.
- [ ] `CURL_UPSTREAM_CASE=1542` passes and 1542 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] Every case on `PassingUpstreamCases.txt` still passes; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: af82bdd2 dd52269f 03ca90eb.
