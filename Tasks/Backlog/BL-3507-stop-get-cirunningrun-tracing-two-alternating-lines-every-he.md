---
id: BL-3507
title: Stop Get-CiRunningRun tracing two alternating lines every heartbeat while a failed job log stays unreadable
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-25
completed:
---
# BL-3507 — Stop Get-CiRunningRun tracing two alternating lines every heartbeat while a failed job log stays unreadable

## Goal

While a running CI run's failed job log stays unreadable, the ci-watch trace logs that run's state once, not two alternating lines on every heartbeat.

## Context

Found by the shift-end review (ADR-0710) of `ee9fe883e...19cfdb2b6`, by the silent-failure-hunter reviewer.

BL-3505 changed `Get-CiRunningRun` in `DarkFactory/CiWatch.ps1` (lines 421-436). When a failed job's log cannot be read, it now skips the failure cache so the next heartbeat retries the read. Two lines are traced under the same key, `running $id` (line 416):

- "cannot read the log of failed job ... skipped" (line 428)
- "running run $id has failed jobs but no failing test or build error in their logs" (line 436)

`Write-CiRunLine` (line 497) suppresses a line only when it equals the last line logged for that key. When every failed log is unreadable, or the readable logs hold no failure, each heartbeat logs both lines. `Logged[$key]` flips between them, so the trace repeats until the log becomes readable. Before BL-3505 the empty result was cached, so these lines were written once.

The second line is also false in this case: it says the logs were read and held no failure. CLAUDE.md requires that code say what it does and do what it says.

## Acceptance criteria

- [ ] When a failed job log was unreadable and no failure was read, `Get-CiRunningRun` returns `$null` without tracing "has failed jobs but no failing test or build error", or traces the unread case under a key of its own.
- [ ] A `-TestCiWatch` self-test calls `Get-CiRunningRun` twice with a log that stays unreadable, and checks that the run's trace line is written on the first call only.
- [ ] The BL-3505 self-test 'a failed job log that could not be read is read again, not cached' still passes.

## Notes

## Log

- 2026-10-25: Created.
