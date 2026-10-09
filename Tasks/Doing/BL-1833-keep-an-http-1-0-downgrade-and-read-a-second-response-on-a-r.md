---
id: BL-1833
title: Keep an HTTP/1.0 downgrade and read a second response on a reused connection so upstream test1074, test1479 and test471 pass (GF-0002)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
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

- [ ] test1074, test1479 and test471 pass in `UpstreamConformanceTests` and are listed in `PassingUpstreamCases.txt`.
- [ ] Each behaviour is pinned by a unit test in `Curl.Protocol.Http.UnitTests`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
