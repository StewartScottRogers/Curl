---
id: BL-1795
title: Close GF-0002: In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1795 — Close GF-0002: In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0002 (In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response), so a later gap analysis measures each of `behaviour:test338`, `behaviour:test1421`, `behaviour:test1134`, `behaviour:test48`, `behaviour:test1418`, `behaviour:test1419`, `behaviour:test435`, `behaviour:test1074`, `behaviour:test1479`, `behaviour:test471` as `match`.

## Context

- Finding: GF-0002, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test338`, `behaviour:test1421`, `behaviour:test1134`, `behaviour:test48`, `behaviour:test1418`, `behaviour:test1419`, `behaviour:test435`, `behaviour:test1074`, `behaviour:test1479`, `behaviour:test471`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test338 actual: <verify><protocol> differs at byte 81 (line 6): expected 'GET /338 HTTP/1.1', got '[DISCONNECT]'. test1421, test1134, test48, test1418 and test1419 have the same shape. test435: <verify><stdout> expected 'local port == [digits]', got 'local port == -1'; the reference curl printed 'local port == 55059'. test1074: expected 'GET /wantmore/10740001 HTTP/1.0', got '... HTTP/1.1' (the downgrade is kept on the reused connection). test1479: expected exit code 8, got 1. test471: expected exit code 8, got 0. Both need the second response on the same connection. Cause in code: InProcessCurl.RunAsync passes no ConnectionCache, so CurlComposition.GroupConnectorOf returns the bare connector and no PoolingConnector (ADR-0050) keeps a connection between requests or URLs. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 338,435,1074,1479

Suggestion, copied from the finding:

In Curl.Console, make InProcessCurl.RunAsync create a run ConnectionCache and pass it to CurlComposition.CreateRunner (runConnections), so GroupConnectorOf wraps the connector in CreatePoolingConnector as the executable does. Report the fake connection's local end point from SwsHttpServerConnector's connections, so %{local_port} is a real number. Mirror the change in Curl.Conformance.UnitTests' RunCurlAsync. Re-measure: a case still failing after this is a product gap in PoolingConnector or HttpProtocolHandler's same-connection handling of HTTP/1.0 and HTTP/0.9 replies.

## Acceptance criteria

- [x] `behaviour:test338`: Curl answers what curl 8.21.0 answers, `upstream test338 passes`, so the item measures `match`.
- [x] `behaviour:test1421`: Curl answers what curl 8.21.0 answers, `upstream test1421 passes`, so the item measures `match`.
- [x] `behaviour:test1134`: Curl answers what curl 8.21.0 answers, `upstream test1134 passes`, so the item measures `match`.
- [x] `behaviour:test48`: Curl answers what curl 8.21.0 answers, `upstream test48 passes`, so the item measures `match`.
- [x] `behaviour:test1418`: Curl answers what curl 8.21.0 answers, `upstream test1418 passes`, so the item measures `match`.
- [x] `behaviour:test1419`: Curl answers what curl 8.21.0 answers, `upstream test1419 passes`, so the item measures `match`.
- [ ] `behaviour:test435`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 248 bytes: HTTP/1.1 200 OK\x0D\x0AContent-Length: 0\x0D\x0A\x0D\x0Alocal port == 55059\x0Alocal ip == 127.0.0.1\x0Aremote_ip == 127.0.0.1\x0Aremote_port == 18990\x0AHTTP/1.1 200 OK\x0D\x0AContent-Length: 0\x0D\x0A\x0D\x0Alocal port == 55060\x0Alocal ip == 127.0.`, so the item measures `match`.
- [ ] `behaviour:test1074`: Curl answers what curl 8.21.0 answers, `upstream test1074 passes`, so the item measures `match`.
- [ ] `behaviour:test1479`: Curl answers what curl 8.21.0 answers, `upstream test1479 passes`, so the item measures `match`.
- [ ] `behaviour:test471`: Curl answers what curl 8.21.0 answers, `upstream test471 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- `InProcessCurl.RunAsync` (connector overload) and `UpstreamConformanceTests.RunCurlAsync` now pass a run `ConnectionCache`, so `CurlComposition.GroupConnectorOf` pools connections as the executable does. In process, test338, 1421, 1134, 48, 1418 and 1419 now pass, and so do test7, 64, 153, 378, 388, 1079, 1095, 1229, 1437, 2061, 2062, 2063, 2076 and 2091; all twenty are added to `PassingUpstreamCases.txt` (1123 passing, none regressed).
- `SwsHttpServerConnector` gives each connection a `LocalEndPoint` of 127.0.0.1 on the next port from 49152 (pinned by `ConnectAsync_TwoConnections_TakeConsecutiveLoopbackLocalPorts`). test435 still prints `local port == -1`: the HTTP side drops the local end point when the remote one is `null`, outside this task's touches - filed as BL-1832.
- test1074 (HTTP/1.0 kept on the reused connection), test1479 (exit 8, got 1) and test471 (exit 8, got 0) are gaps in `HttpProtocolHandler`, outside this task's touches - filed as BL-1833.
- Decision: Done rather than Backlog, because the pooling change is complete and the four remaining items are separate product gaps carried by BL-1832 and BL-1833; GF-0002 closes only on a re-measure anyway (ADR-0433). No option changed, so `--ai-help` is unaffected.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
