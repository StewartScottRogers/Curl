---
id: BL-4385
title: Name the cause of the CI watch first-heartbeat list failure from a shift log
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-4384]
touches: [DarkFactory/CiWatch.ps1, DarkFactory/ShiftEnd.ps1, DarkFactory/Heartbeat.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4385 — Name the cause of the CI watch first-heartbeat list failure from a shift log

## Goal

The cause of the CI watch's first-heartbeat list failure is named from a shift log taken after BL-4384's change, and fixed if it is the factory's own.

## Context

Split from BL-4377 (its criterion 4). Likely cause, from reproduction: the timed `gh` wrapper space-joined `--json a,b`, so gh exited 1 with `Unknown JSON field`; BL-4384 fixes it. A lane cannot run a shift, so this needs a shift log written after BL-4384's commits are on the shift branch.

## Acceptance criteria

- [x] The first shift log after BL-4384's commits is read (`DarkFactory-*.log`), and Notes name the cause: either it has no `cannot list the CI runs` line (or one followed by `listing CI runs again`), confirming the wrapper cause, or the line's exit code and gh stderr name the real cause.
- [x] If the cause is the factory's own and not already fixed, it is fixed with a test, and `RunDarkFactory.ps1 -TestCiWatch` reports 0 FAIL.

## Notes

- Split from BL-4377.
- 2026-10-31 (interactive): BL-4384's change landed as 8c14126f5 (committed from BL-4377's stash, -TestCiWatch 78, -TestHeartbeat 23, -TestCoordinatorWatchdog 16 checks, 0 FAIL under Windows PowerShell and pwsh). Cause confirmed: the timed gh wrapper space-joined `--json a,b`. DarkFactory-20261031-060544.log still wrote the old bare `cannot list the CI runs on work/dark-factory with gh` at 06:06:43, because that coordinator loaded its scripts at 06:05:44 and only fast-forwarded the checkout to 8c14126 at 06:05:46. The next shift, DarkFactory-20261031-060833.log, loaded 8c14126 and at 06:09:08 listed the CI runs (`run 41194895834 success on bfe08061: nothing to file` and four more) with no `cannot list` line. Fixed already by 8c14126; the load-before-sync gap is filed as its own task.
- If no shift has run since BL-4384 landed, send this task to Blocked naming that shift as the blocker.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: fc9fd4c6 111a9fbf.
