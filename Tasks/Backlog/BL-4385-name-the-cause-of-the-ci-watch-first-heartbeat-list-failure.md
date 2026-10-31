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

- [ ] The first shift log after BL-4384's commits is read (`DarkFactory-*.log`), and Notes name the cause: either it has no `cannot list the CI runs` line (or one followed by `listing CI runs again`), confirming the wrapper cause, or the line's exit code and gh stderr name the real cause.
- [ ] If the cause is the factory's own and not already fixed, it is fixed with a test, and `RunDarkFactory.ps1 -TestCiWatch` reports 0 FAIL.

## Notes

- Split from BL-4377.
- If no shift has run since BL-4384 landed, send this task to Blocked naming that shift as the blocker.

## Log

- 2026-10-31: Created.
