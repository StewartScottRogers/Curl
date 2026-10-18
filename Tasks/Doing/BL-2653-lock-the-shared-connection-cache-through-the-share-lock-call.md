---
id: BL-2653
title: Lock the shared connection cache through the share lock callbacks so upstream case 1554 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2653 — Lock the shared connection cache through the share lock callbacks so upstream case 1554 passes

## Goal

Upstream case 1554 (`lib1554`, HTTP with a shared connection cache) passes through the runner and is on the ratchet.

## Context

Found by BL-2541 (lib1554 port). With `CURLSHOPT_SHARE CURL_LOCK_DATA_CONNECT` and lock/unlock callbacks set, upstream libcurl locks and unlocks `CURL_LOCK_DATA_CONNECT` around every use of the shared connection pool and `CURL_LOCK_DATA_SHARE` around attach and detach. Curl's engine (`ManagedShareHandle`) calls the callbacks for SHARE only, so case 1554's stdout differs at byte 88 (line 5): expected `-] Mutex lock CONNECT`, got `run 1: foobar and so on fun!`. The full expected order is case 1554's `<datacheck>`; match libcurl 8.22.0's `conncache.c`/`share.c` lock points.

## Acceptance criteria

- [ ] The engine calls the share's lock and unlock callbacks for `CURL_LOCK_DATA_CONNECT` at the points libcurl 8.22.0 does, with a focused unit test in `Curl.Engine.UnitTests`.
- [ ] Case 1554 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 63105f4e 52abb3eb 4272a6b9.
