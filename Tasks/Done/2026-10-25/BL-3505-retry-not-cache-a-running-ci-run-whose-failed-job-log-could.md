---
id: BL-3505
title: Retry, not cache, a running CI run whose failed job log could not be read
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3505 — Retry, not cache, a running CI run whose failed job log could not be read

## Goal

When `gh` cannot read the log of a failed job in a CI run that is still running, the coordinator's CI watch tries that log again on its next heartbeat, so the run's failing tests are still filed early (BL-3454).

## Context

Found by the shift-end review of d5489de4..287e5bf6 (ADR-0710): the code-reviewer and silent-failure-hunter both reported it.

`DarkFactory/CiWatch.ps1`, `Get-CiRunningRun` (around lines 425-432). The shift changed `if ($null -eq $log) { return $null }` to a trace line and `continue`. The loop then caches its result in `$script:CiWatch.Failures[$cacheKey]`. That key is built only from the run ID and the IDs of the failed jobs, and nothing clears it. So if a log read fails once (for example a 404 just after the job ends), the run's failures are cached without that job's log, and later heartbeats never read it again. With one failed job, every heartbeat then logs "running run N has failed jobs but no failing test or build error in their logs", which is false, and no "Fix CI failure" task is filed until the run finishes.

## Acceptance criteria

- [x] In `Get-CiRunningRun`, a failed job whose log cannot be read leaves no entry in `$script:CiWatch.Failures` for that set of failed jobs, so the next call reads the log again. The trace line saying the log could not be read stays.
- [x] A `-TestCiWatch` case shows it: a `$ReadJobLog` stub that returns `$null` once and then a log with a failing test gives `$null` on the first call and that failure on the second.
- [x] `powershell -NoProfile -File RunDarkFactory.ps1 -TestCiWatch` passes.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: a2de3ed1 4abf9b60 cfd9f717.
- 2026-10-25: Doing -> Done. Unread failed-job log is no longer cached; retried next heartbeat
