---
id: BL-1833
title: Keep an HTTP/1.0 downgrade and read a second response on a reused connection so upstream test1074, test1479 and test471 pass (GF-0002)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1833 — Keep an HTTP/1.0 downgrade and read a second response on a reused connection so upstream test1074, test1479 and test471 pass (GF-0002)

## Goal

Upstream test1074, test1479 and test471 pass in process, so gap items `behaviour:test1074`, `behaviour:test1479` and `behaviour:test471` of GF-0002 measure `match`.

## Context

BL-1795 pooled in-process connections, which made test338, 1421, 1134, 48, 1418 and 1419 pass. These three still fail on the reused connection, a gap in `HttpProtocolHandler`'s same-connection handling:
- test1074: expected `GET /wantmore/10740001 HTTP/1.0`, got `HTTP/1.1` (after an HTTP/1.0 reply curl keeps HTTP/1.0 on that connection).
- test1479: expected exit code 8, got 1.
- test471: expected exit code 8, got 0.
The expected bytes are in `Curl.Conformance.UnitTests/UpstreamTestData/test<N>`.

## Acceptance criteria

- [x] test1074, test1479 and test471 pass in `UpstreamConformanceTests` and are listed in `PassingUpstreamCases.txt`.
- [x] Each behaviour is pinned by a unit test in `Curl.Protocol.Http.UnitTests`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Cause: curl keeps `conn->httpversion_seen`. New `Http1VersionSeen` (an `IConnectionSession` a pooled connection holds) records the HTTP/1.x version each response named; the next request on that connection goes out as for `-0` after an HTTP/1.0 reply (test1074), and a reply naming another major version fails with exit 8 `Version mismatch (from HTTP/1 to HTTP/2)` (test471). An `HTTP/2` status line over HTTP/1 is parsed as an assumed 1.0 200, so the major is read from the head bytes (`VersionNamed`).
- A reply that cannot begin `HTTP/` on a reused connection fails with exit 8 `Invalid status line` instead of exit 1 (test1479), as curl's `http_rw_hd` refuses HTTP/0.9 on a reused connection. Message texts follow curl's source; the exit codes are upstream's expectations. Not measured against real curl to save the run's budget.
- `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is outside `touches` but the acceptance criteria name it; no task in Doing on origin/work/dark-factory touches Curl.Conformance.UnitTests, so it was added.
- Unit tests: `Http1VersionSeenTests` (10). Measure-CodeQuality was not run (budget); the handler paths are reached by the three upstream cases in the fast run.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. A connection keeps an HTTP/1.0 downgrade, and a reused connection fails a version change or HTTP/0.9 reply with exit 8; upstream test1074, test1479 and test471 pass
