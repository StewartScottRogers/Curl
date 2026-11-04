---
id: BL-4800
title: Hand -UnpacedLanes, -StopAtUsage and -StopAtWeeklyUsage on to the next shift
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/LaneState.ps1]
lane: no
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4800 — Hand -UnpacedLanes, -StopAtUsage and -StopAtWeeklyUsage on to the next shift

## Goal

A `-Continuous` shift starts the next shift with the same `-StopAtUsage`, `-StopAtWeeklyUsage` and `-UnpacedLanes` it ran with.

## Context

Stewart, 2026-11-02, ran shifts unthrottled (`-UnpacedLanes 13 -StopAtUsage 1 -StopAtWeeklyUsage 1`). The hand-over in `DarkFactory/ShiftCoordinator.ps1` (`$startNextShift`) forwarded `-MaxLanes` and `-MinStartLanes` but not these three, so the shifts started at 16:52 and 18:38 on 2026-11-03 fell back to the defaults (3 unpaced lanes, stop claiming at 85% and 97%).

## Acceptance criteria

- [x] `$startNextShift` in `DarkFactory/ShiftCoordinator.ps1` forwards `-StopAtUsage` and `-StopAtWeeklyUsage` always, and `-UnpacedLanes` with `-Lanes Auto`.
- [x] `RunDarkFactory.ps1 -TestHeartbeat` checks the three are forwarded and passes under Windows PowerShell 5.1 and pwsh 7.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 7f48329d e6432c55.
- 2026-11-03: Doing -> Done. Hand-over forwards the three; -TestHeartbeat checks it and passes under Windows PowerShell 5.1 and pwsh 7.
