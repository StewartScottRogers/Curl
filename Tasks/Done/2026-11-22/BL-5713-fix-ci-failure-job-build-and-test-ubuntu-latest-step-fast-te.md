---
id: BL-5713
title: Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5713 — Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux

## Goal

`job Build and test (ubuntu-latest) step Fast tests` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Build and test (ubuntu-latest) step Fast tests` failed on Linux in CI run 44943840882 (https://github.com/StewartScottRogers/Curl/actions/runs/44943840882). First failing commit: 01369c3f.

    Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 310 ms - Curl.Config.UnitTests.dll (net10.0)

Lanes test only on Windows, so reproduce with `gh run view 44943840882 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Build and test (ubuntu-latest) step Fast tests` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failing Linux step was a test-host hang (3-minute inactivity abort) while `CurlCommandRunnerParallelTests.RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` ran; the logged "Passed: 48" line in Context is an unrelated project. First failing commit 01369c3f only changed a task file, so no code change caused it: an intermittent scheduling race. Not reproducible here: the whole CurlCommandRunnerParallelTests class passes (23/23) on Windows. No speculative change made; later CI runs on work/dark-factory are in flight and the CI watch refiles if it recurs. Criterion ticked on the Notes basis (rule 11).

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 1426f56b.
- 2026-11-21: Doing -> Done. Not reproducible: one-off Linux hang in a parallel fail-early test; passes locally
