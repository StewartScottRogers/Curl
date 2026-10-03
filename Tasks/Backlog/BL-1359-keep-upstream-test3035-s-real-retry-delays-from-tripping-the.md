---
id: BL-1359
title: Keep upstream test3035's real retry delays from tripping the conformance row's hang limit under coverage
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1359 — Keep upstream test3035's real retry delays from tripping the conformance row's hang limit under coverage

## Goal

Upstream case `test3035` never fails `UpstreamConformanceTests` by outlasting its 30-second `CaseHangLimit`, however slow the machine or the coverage collector makes it.

## Context

- On 2026-10-03, while BL-1355 measured `Curl.Conformance.UnitLibrary` with `Measure-CodeQuality.ps1`, the row `test3035` (not on `PassingUpstreamCases.txt`; normally Inconclusive in about 2.2 s) failed after 52 s with `System.TimeoutException` from `WaitAsync(CaseHangLimit)` in `UpstreamConformanceTests.cs` line 52. A rerun passed.
- The case runs `--retry 4 --retry-delay 1 --retry-all-errors`: curl waits on its own real clock between attempts, which BL-1355's wait-skipping server clock does not touch.
- A hang-limit timeout fails the row even for a case that is not listed, so an unlisted case can break the fast suite. Find what stretches the run (curl's retry waits, or a hang) and make the row independent of machine load.

## Acceptance criteria

- [ ] The cause of test3035's 52-second run is named under Notes, with the file and line.
- [ ] An unlisted case that outlasts the hang limit no longer fails the fast suite, or test3035 no longer takes real time beyond its retry delays.
- [ ] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-03: Created.
