---
id: BL-3439
title: Hold the case 1182 conformance code to the quality gates and the solution fast tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3410, BL-3438]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1169Script.cs, Curl.Conformance.UnitLibrary/UpstreamShellCheckLine.cs, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamShellCheckLineTests.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3439 — Hold the case 1182 conformance code to the quality gates and the solution fast tests

## Goal

The case 1182 code BL-3438 landed is within every quality gate and the solution's fast tests pass.

## Context

Split from BL-3412 (ADR-0803, BL-3406 decides 1182 as `interpret`). Upstream runner: https://github.com/curl/curl/blob/curl-8_22_0/tests/runtests.pl (`-E`, `exclude`). The case is `Curl.Conformance.UnitTests/UpstreamTestData/test1182.curltest`; every path the precheck names stays inside the case's log directory, and `cp %SRCDIR/data/test1` copies from the vendored `UpstreamTestData`. BL-3410 registers the `runtests.pl` interpretation (`UpstreamTest1169Script`); this extends it.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no member past a gate (100% line and branch coverage, complexity at most 10, CRAP at most 30), with any gap closed by unit tests in `Curl.Conformance.UnitTests`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the whole solution.

## Notes

Split from BL-3412, which hit its cost cap. Covers BL-3412's last two criteria; the code itself lands in BL-3438.

2026-10-23 (lane 3): `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reported 0 failing members, so the case 1182 code BL-3438 landed already meets every gate and no test was needed. `dotnet build` clean (0 warnings) and the solution fast tests green on Windows; the change set is test-data and runner code with no platform-dependent output, so CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 13ae43a4 34c8894d.
- 2026-10-23: Doing -> Done. Case 1182 conformance code is within every quality gate and the solution fast tests pass
