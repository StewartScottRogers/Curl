---
id: BL-5418
title: Forward -StopAtUsage and -StopAtWeeklyUsage from the coordinator to every lane
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1]
lane: no
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5418 — Forward -StopAtUsage and -StopAtWeeklyUsage from the coordinator to every lane

## Goal

Lanes stop claiming at the shift's own `-StopAtUsage` and `-StopAtWeeklyUsage`, not at their defaults (85% and 97%).

## Context

The shift of 2026-11-17 (`20261117-194355`) was started with `-StopAtUsage 1 -StopAtWeeklyUsage 1`
(the unthrottled shape Stewart approved on 2026-11-02), and its coordinator held those values. But
`$laneArgsFor` in `DarkFactory/ShiftCoordinator.ps1` built each lane's command line without them, so
every lane ran with the parameter defaults and 11 of 13 lanes stopped at "weekly tokens 97% used".
BL-4800 already hands them to the next shift; this task hands them to the lanes too.

## Acceptance criteria

- [x] `$laneArgsFor` in `DarkFactory/ShiftCoordinator.ps1` passes `-StopAtUsage` and `-StopAtWeeklyUsage` with the shift's values (invariant culture).
- [x] `RunDarkFactory.ps1 -TestHeartbeat` has a case "every lane is handed the shift's -StopAtUsage and -StopAtWeeklyUsage (BL-5418)" that passes, and the whole self-test exits 0.

## Notes

- Interactive only (`lane: no`): it changes how the running shift's lanes are started, and the fix takes effect through `RunDarkFactory.cmd -Restart`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 590a0e15 ad14a286.
- 2026-11-17: Doing -> Done. Lanes now get the shift's -StopAtUsage and -StopAtWeeklyUsage; -TestHeartbeat passes
