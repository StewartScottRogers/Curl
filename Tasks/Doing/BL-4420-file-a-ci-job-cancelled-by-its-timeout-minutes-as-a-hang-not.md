---
id: BL-4420
title: File a CI job cancelled by its timeout-minutes as a hang, not ignore it as cancelled
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4420 — File a CI job cancelled by its timeout-minutes as a hang, not ignore it as cancelled

## Goal

When a `CI` job on the shift branch is cancelled because it ran into its `timeout-minutes` (a hang), the coordinator's CI watch files a High "Fix CI hang ..." task naming the job, the run, the commit and the test assembly that was running, instead of ignoring the run as cancelled.

## Context

Run 41217938101 on fe634e2c (2026-10-31): `Quality gates shard 3 of 3 (windows-latest)` started 09:10 Arizona, printed its last test line at 09:16 (`Test run for ...Curl.Console.UnitTests.dll`, after `Curl.Conformance.SshServer.UnitTests` passed), then nothing until `##[error]The operation was canceled.` at 09:55, its 45-minute `timeout-minutes`. GitHub reports the job and the run as `cancelled`, which the CI watch skips (cancelled runs prove nothing, BL-987), so nothing was filed, and the shift-end merge gave up with `not merged: CI cancelled on fe634e2`. A run cancelled by a newer push (or a waiting run replaced before any job started, which has 0 jobs) is a different thing and stays ignored.

A job cancelled by its timeout can be told apart from one cancelled by a person or a newer run: it started, and `completed_at - started_at` is at least its `timeout-minutes` (from `.github/workflows/ci.yml`, read-only to a lane: take the value from the job's log, `The job running on runner ... has exceeded the maximum execution time of N minutes`, or from the gh jobs API), or its log's last step ends with `##[error]The operation was canceled.` after a silence of many minutes. Pick the test that is cheapest to make reliable and say which in Notes.

## Acceptance criteria

- [ ] A CI run whose job was cancelled by its timeout is examined, not skipped, and files one High task titled `Fix CI hang in <job> (<assembly>)` with the run, commit, job, the last `Test run for` assembly, and the time of the last log line before the silence.
- [ ] A run cancelled by a newer push, by a person, or before any job started still files nothing.
- [ ] A hang already named by a live task is not filed again (as for failing tests).
- [ ] `RunDarkFactory.ps1 -TestCiWatch` covers the three cases with stand-in `-ListRuns`, `-ReadJobs` and `-ReadLog` blocks and reports 0 FAIL under Windows PowerShell and `pwsh`.

## Notes

- 2026-10-31 (interactive): not reproduced locally. `Curl.Console.UnitTests` at ad7f977c with `--blame-hang-timeout 3m`, three runs, each 3104 passed in 3 s (without coverage collection). The next CI run (bb37b9b3) passed every job, so the hang is intermittent; a filed hang task should say so.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 258b24e8 3c8fb19c 6a9f3801 95af59d2.
