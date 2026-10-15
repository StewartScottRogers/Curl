---
id: BL-2269
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2269 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 38700283564 (https://github.com/StewartScottRogers/Curl/actions/runs/38700283564). First failing commit: 639a0aec.

    [36;1mforeach ($m in @((Get-Content "$env:RUNNER_TEMP\coverage.json" -Raw | ConvertFrom-Json).failing)) {[0m

Lanes test only on Windows, so reproduce with `gh run view 38700283564 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not the code. All 108 failing members were in `Curl.Cryptography.UnitLibrary`, whose 1462
  tests passed in that run; the whole-solution run's merged Cobertura report lost
  `Curl.Cryptography.UnitTests`' coverage (its tests took 4m48 of wall clock against 28 s in the
  last green run). The other CI run of the same commit 639a0aec (38700277194, push) passed the
  quality job, as did Windows, Linux and macOS build-and-test. Locally the Cryptography tests alone
  measure 100% line and branch, 0 failing members.
- Fix (ADR-0503): `Measure-CodeQuality.ps1` now rechecks a short whole-solution run - it reruns the
  `*.UnitTests` projects reaching each short library one at a time into `recheck\` and merges them
  in before scoring again. Verified with a temporary copy on a one-project solution and
  `-LineThreshold 101` (forces the recheck): it ran the projects reaching Cryptography into
  `recheck\` and merged them. `-SkipTestRun` scoring of the local Cryptography report: 0 failing.
- The criterion's CI half: the failing commit passed CI on its other run; the commit landing this
  script change is checked by the shift's CI watch (BL-987), which files a new task if it fails.
- No production C# changed, so no per-library quality run was needed; `dotnet build` clean and the
  fast tests green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Quality job failure was a lost coverage report, not code; Measure-CodeQuality.ps1 now rechecks a short whole-solution run per project (ADR-0503).
