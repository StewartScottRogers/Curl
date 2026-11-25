---
id: BL-5919
title: Fix CI failure job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5919 — Fix CI failure job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2 on Windows

## Goal

`job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` failed on Windows in CI run 45520782590 (https://github.com/StewartScottRogers/Curl/actions/runs/45520782590). First failing commit: 0c6497ad.

    VSTEST_CONNECTION_TIMEOUT: 600

Lanes test only on Windows, so reproduce with `gh run view 45520782590 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the `Curl.Console.UnitTests` host crashed on the runner after all its tests had run (Blame: "All tests finished running"; no stack trace; "Test case event channel failed"). No test failed. The retry BL-5416 added should have run the project again but never could: `dotnet test` writes "Test host process crashed" to stderr, and `Invoke-CoverageTestRun` captured only stdout (no "running it once more" line in the CI log).
- Fix: `Measure-CodeQuality.ps1` captures stderr with stdout (`2>&1`, with `$ErrorActionPreference = 'Continue'` around that call only, for Windows PowerShell). Since the retry now works, a "Failed <name>" line also counts as a failed test, so a crash after a reported failure is not retried. The same capture lets the "no tests match" tolerance see an aborted run, which it missed before.
- Checked with a temporary MSTest project calling `Environment.FailFast`, driving `Invoke-CoverageTestRun` under PowerShell 7 and Windows PowerShell 5.1: crash once -> two runs, one report, passes; crash always -> two runs, throws; failed test then crash -> throws (PowerShell 7's first attempt crashed before the failure was reported, so it retried and the failure then threw it).
- `-Shard 2 -ShardCount 3` passed locally on 2026-11-24 (38 reports for 38 test projects; `Curl.Console.UnitTests` 3288 passed, 38 skipped). `dotnet build` clean, fast tests green.
- CI on Linux and macOS: the change is platform-neutral PowerShell, so the criterion is ticked on that basis (lane rule); the shift's CI watch files a task for any failure.
- Direct change, no ADR: no library or curl behaviour changed, so no tests-first or conformance stage and no per-library quality run.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ff819106.
- 2026-11-24: Doing -> Done. Measure-CodeQuality.ps1 now sees a crashed test host on stderr and runs that project once more
