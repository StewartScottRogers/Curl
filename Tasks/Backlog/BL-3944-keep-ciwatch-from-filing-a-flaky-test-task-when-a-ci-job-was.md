---
id: BL-3944
title: Keep CiWatch from filing a flaky-test task when a CI job was never given a runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-28
completed:
---
# BL-3944 — Keep CiWatch from filing a flaky-test task when a CI job was never given a runner

## Goal

A failed CI run where every failed or cancelled job only failed because no runner picked it up ("The job was not acquired by Runner of type hosted even after multiple attempts") leads CiWatch to log a runner-capacity line, not to file a `job unknown job step unknown step` flaky-test task.

## Context

BL-3943 was filed by CiWatch for CI run 40709122071 (commit 49687722). Every job passed
except `Pack Curl.Mcp (macos-latest)`, which was cancelled after 18 minutes with no step
run. Its check-run annotations (`gh api repos/StewartScottRogers/Curl/check-runs/122890389556/annotations`)
read "The job was not acquired by Runner of type hosted even after multiple attempts"
and "Due to capacity constraints, jobs targeting macOS arm64 runners may experience
longer queue times." Because `gh run view --log-failed` printed nothing, the AF-0178 fallback in
`DarkFactory/CiWatch.ps1` (around line 360) filed the run as `job unknown job step unknown step`.
That is a GitHub runner-capacity failure, so no test or code in the repository can fix it.

Where to start: when `--log-failed` is empty, read the run's jobs (`gh run view <id> --json jobs`).
If every job that did not succeed is `cancelled` with no step run, or has the runner-acquisition
annotation, record it as runner capacity and don't file a task. Add a self-check case beside
the AF-0178 ones (around line 945).

## Acceptance criteria

- [ ] When a failed run's only non-successful jobs were never given a runner, CiWatch files no task and writes a line naming runner capacity; a CiWatch self-check case covers this.
- [ ] When a failed run has an empty `--log-failed` and a job that did run and fail, CiWatch still files it as before (AF-0178 self-checks still pass).

## Notes

## Log

- 2026-10-28: Created.
