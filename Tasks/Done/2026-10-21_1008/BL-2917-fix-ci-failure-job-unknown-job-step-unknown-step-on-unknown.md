---
id: BL-2917
title: Fix CI failure job unknown job step unknown step on unknown job
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2917 — Fix CI failure job unknown job step unknown step on unknown job

## Goal

`job unknown job step unknown step` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job unknown job step unknown step` failed on unknown job in CI run 39623036192 (https://github.com/StewartScottRogers/Curl/actions/runs/39623036192). First failing commit: cdc38efa.

    the run failed and gh run view --log-failed printed no line

Lanes test only on Windows, so reproduce with `gh run view 39623036192 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job unknown job step unknown step` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Measured 2026-10-21: run 39623036192 (cdc38efa) failed only because its
  `Pack Curl.Mcp (macos-latest)` job was cancelled at the job's 30-minute `timeout-minutes`
  with no step ever started (`steps: []`, no log, so `--log-failed` printed nothing). Every
  other job passed, including Build and test on all three platforms. The runner never ran
  the job: a GitHub-hosted macOS runner fault, not a test or build failure.
- The next finished run on the branch, 39628455973 at 29dcc2f4 (which contains cdc38efa),
  passed every job; its macOS pack job took 11 minutes, in line with 7-9 minutes in the
  earlier green runs 39620703250 and 39620108015. No code or workflow change is needed, so
  this task lands no fix commit; the criterion is met by run 39628455973.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 15da4c4d.
- 2026-10-21: Doing -> Done. Transient macOS runner fault (job cancelled at timeout with no step run); run 39628455973 at 29dcc2f4, which contains cdc38efa, is green on all three platforms
