---
id: BL-1054
title: Log why a lane waits, not task-board.ps1's duplicate-ID warning, and clear the BL-806 duplicate that causes it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1054 â€” Log why a lane waits, not task-board.ps1's duplicate-ID warning, and clear the BL-806 duplicate that causes it

## Goal

Every lane `wait` line in the dark factory's trace says why the lane is idle (the `No task is ready.` or `No task can start yet: ...` line from `task-board.ps1 next`, or a lost claim race), so the process metrics can tell overlap waits from an empty queue.

## Context

Found while delivering BL-1008 (2026-09-30). Of the lane `wait` lines since 2026-09-29, 267 read `WARNING: Duplicate task ID BL-806: ...` rather than the reason: the lane logs the first line of `task-board.ps1 next`'s output, and the script prints a duplicate-ID warning first while two task files share BL-806 (one in Backlog, one archived under `Tasks/Done/2026-09-28_1849/`). So `Audit/Tools/Measure-FactoryProcess.ps1` counts all 324 idle minutes as `waitOtherMinutes`, none as overlap or nothing-ready.

Two fixes, both needed: the lane should log the line that answers the question (skip `WARNING:` lines and their continuation lines when choosing what to log), and the Backlog BL-806 should be renumbered so the warning stops (the archived one is final and keeps its ID). `RunDarkFactory.ps1` is not a guarded path, so a lane may do this.

## Acceptance criteria

- [x] A lane that waits logs `wait` with the reason line from `next`, not a `WARNING:` line, even while a duplicate-ID warning is printed; a `-Test*` case proves it on recorded `next` output.
- [x] The Backlog task holding BL-806 has a new ID, with its references updated, and `task-board.ps1 status` prints no duplicate-ID warning.

## Notes

- New Get-WaitReason in RunDarkFactory.ps1 picks next's 'No task ...' line; with none it drops WARNING: lines and the 'Tasks\...' paths a duplicate-ID warning wraps onto. Invoke-Claim logs that as the wait reason. Three -TestTaskIds cases prove it on recorded next output, including the real wrapped BL-806 warning.
- The Backlog BL-806 (STARTTLS -v TLS lines) is now BL-1055, the next free ID; the archived Done BL-806 keeps its ID, and ADR-0130's BL-806 references are to that one, so nothing else changed. Touching Tasks/Backlog is board housekeeping the task asks for, not a new project.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Lane wait lines give next's reason, not a duplicate-ID warning; Backlog BL-806 renumbered BL-1055
