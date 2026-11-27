---
id: BL-6075
title: Stop InProcessCurl_ConnectionReuseCase_Passes timing out after 20 seconds in a loaded test run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.ConnectionReuseCases.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6075 — Stop InProcessCurl_ConnectionReuseCase_Passes timing out after 20 seconds in a loaded test run

## Goal

`InProcessCurl_ConnectionReuseCase_Passes` passes in a whole-solution fast test run on a loaded machine.

## Context

On 2026-11-26, in BL-6073's whole-solution `dotnet test` on lane 12 with other lanes building,
cases (471,False), (435,False), (1074,False), (338,False) and (1074,True) failed after 22 s with
`curl did not finish within 20 seconds`. All 20 cases passed in 1 s when run alone. Not seen in CI.
Start at `Curl.Conformance.UnitTests/UpstreamConformanceTests.ConnectionReuseCases.cs` and the time
limit `UpstreamCaseRunner` is given.

## Acceptance criteria

- [ ] The cause of the 20 second stall (a starved thread pool, a blocking wait, or only too short a limit) is recorded under Notes.
- [ ] The reuse cases no longer fail on a time limit a loaded run exceeds, and they pass.

## Notes

## Log

- 2026-11-26: Created.
