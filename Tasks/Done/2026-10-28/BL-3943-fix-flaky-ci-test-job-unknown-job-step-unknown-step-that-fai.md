---
id: BL-3943
title: Fix flaky CI test job unknown job step unknown step that failed once on unknown job
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3943 — Fix flaky CI test job unknown job step unknown step that failed once on unknown job

## Goal

`job unknown job step unknown step` passes on every run on Windows, Linux and macOS; it failed once on unknown job in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job unknown job step unknown step` failed on unknown job in CI run 40709122071 (https://github.com/StewartScottRogers/Curl/actions/runs/40709122071). First failing commit: 49687722.

    the run failed and gh run view --log-failed printed no line

Lanes test only on Windows, so reproduce with `gh run view 40709122071 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job unknown job step unknown step` fail intermittently is named under Notes and removed.
- [x] `job unknown job step unknown step` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: in run 40709122071 every job succeeded except `Pack Curl.Mcp (macos-latest)`,
  which GitHub cancelled after 18 minutes with no step run. Its check-run annotations say
  "The job was not acquired by Runner of type hosted even after multiple attempts" and
  "Due to capacity constraints, jobs targeting macOS arm64 runners may experience longer
  queue times". So no test failed: GitHub had no free hosted macOS runner. With no failed
  step, `--log-failed` was empty, and CiWatch's AF-0178 fallback filed the run as
  `job unknown job step unknown step`.
- Removed: nothing in the repository caused it and no code change can prevent it
  (`.github/workflows/ci.yml` is also out of a lane's reach). What can be removed is the
  false report: BL-3944 makes CiWatch tell a run that was never given a runner apart from a
  real failure, so it stops filing tasks like this one.
- Locally: `dotnet build` clean and the fast tests green on Windows (0 failed). This task
  changed no code, so nothing differs by platform. The job passed on the CI runs before
  and after this one, and the shift's CI watch reports any new failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 024f206a 35faa999.
- 2026-10-28: Doing -> Done. Cause named: a GitHub macOS runner-capacity cancellation, not a test; BL-3944 filed to stop CiWatch filing such runs
