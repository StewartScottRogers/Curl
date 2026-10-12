---
id: BL-2073
title: Re-close GF-0002: In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2073 — Re-close GF-0002: In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0002 (In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response), so a later gap analysis measures each of `behaviour:test338`, `behaviour:test1421`, `behaviour:test1134`, `behaviour:test48`, `behaviour:test1418`, `behaviour:test1419`, `behaviour:test435`, `behaviour:test1074`, `behaviour:test1479`, `behaviour:test471` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0002 ([BL-1795, BL-1998]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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
- [x] `behaviour:test435`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 248 bytes: HTTP/1.1 200 OK\x0D\x0AContent-Length: 0\x0D\x0A\x0D\x0Alocal port == 57871\x0Alocal ip == 127.0.0.1\x0Aremote_ip == 127.0.0.1\x0Aremote_port == 18990\x0AHTTP/1.1 200 OK\x0D\x0AContent-Length: 0\x0D\x0A\x0D\x0Alocal port == 57872\x0Alocal ip == 127.0.`, so the item measures `match`.
- [x] `behaviour:test1074`: Curl answers what curl 8.21.0 answers, `upstream test1074 passes`, so the item measures `match`.
- [x] `behaviour:test1479`: Curl answers what curl 8.21.0 answers, `upstream test1479 passes`, so the item measures `match`.
- [x] `behaviour:test471`: Curl answers what curl 8.21.0 answers, `upstream test471 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-11 (lane 1, at 6f57301f6): the finding's suggested fix is already in the product.
  `InProcessCurl.RunAsync` (both overloads) passes `runConnections: new ConnectionCache(...)`
  since 7ae8cbcf8 (2026-10-08), so `GroupConnectorOf` wraps the connector in a `PoolingConnector`.
  `UpstreamConformanceTests.InProcessCurl_ConnectionReuseCase_Passes` runs all ten items
  (48, 338, 435, 471, 1074, 1134, 1418, 1419, 1421, 1479) through the public `InProcessCurl`.
  It runs each over the in-memory connector and over the TcpConnector. All 20 rows pass here.
  So whatever still measures the gap is outside Curl.Console and Curl.Conformance.
  Two candidates remain:
  - the gap run measured a ref older than 7ae8cbcf8;
  - `Gap/Tools/Measure-UpstreamCases.cs` calls Curl differently, e.g. through its own connector
    or an older overload.
  A lane cannot read or run `Gap/` (the audit guard refuses it, ADR-0433), so this is interactive only.
- Remove `lane: no` once an interactive session has run
  `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs` for 48,338,435,471,1074,1134,1418,1419,1421,1479
  on the current `work/dark-factory`. If every item now matches, close GF-0002 on the next gap run
  and finish this task. If items still fail, write under Notes how the tool's call differs from
  `InProcessCurl_ConnectionReuseCase_Passes`, so that a lane can reproduce the failure in Curl.Conformance.UnitTests.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. Interactive only: all ten items pass through InProcessCurl in UpstreamConformanceTests; what still fails is the gap office's measuring tool or a stale gap run, which a lane cannot read

Closed by evidence: the fixed gap harness (PR #119, merged 2026-10-12) re-measured every item of this task as `match` with the gap office's own Measure-UpstreamCases tool. The finding itself closes at the next gap run.
- 2026-10-11: Backlog -> Doing. PR #119 re-measured every item as match
- 2026-10-11: Doing -> Done. PR #119 re-measured every item as match
