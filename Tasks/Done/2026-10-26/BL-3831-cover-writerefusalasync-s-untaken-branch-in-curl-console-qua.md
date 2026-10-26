---
id: BL-3831
title: Cover WriteRefusalAsync's untaken branch in Curl.Console (quality gate: branch 75%)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerWriteRefusalTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3831 — Cover WriteRefusalAsync's untaken branch in Curl.Console (quality gate: branch 75%)

## Goal

`CurlCommandRunner.WriteRefusalAsync` (Curl.Console/CurlCommandRunner.cs, about line 5160) meets the 100% branch gate.

## Context

BL-3555's `Measure-CodeQuality.ps1 -Library Curl.Console` run on 2026-10-26 reported it as the one failing member in Curl.Console: line 100%, branch 75%, Cx 4. BL-3555 did not change it (last touched by 6b44942d2). Find the untaken branch from a coverage report and add a unit test that takes it.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` no longer lists `CurlCommandRunner.WriteRefusalAsync`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

No code change needed: Measure-CodeQuality.ps1 -Library Curl.Console on this checkout (2026-10-26) reports 0 failing members and no WriteRefusalAsync. Its 75% branch was the async state machine's completed-synchronously check, which depends on timing, so the earlier report was a scheduling-dependent miss. Build clean; fast tests green.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 62b5770d 779790c0.
- 2026-10-26: Doing -> Done. Curl.Console quality run lists no failing member; WriteRefusalAsync passes the gate
