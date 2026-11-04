---
id: BL-4826
title: Let a lane that ended with nothing ready leave the active set, so Auto adds lanes back when work appears
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/LaneState.ps1]
lane: no
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4826 — Let a lane that ended with nothing ready leave the active set, so Auto adds lanes back when work appears

## Goal

A lane that ends with nothing ready leaves the coordinator's active set, so `-Lanes Auto` adds lanes back when ready work appears later in the same shift.

## Context

Stewart, 2026-11-04 ("always function in a highly parallel fashion"; approved this fix). BL-4824 (aef2fa6d2) made a lane with nothing to claim end instead of waiting. The coordinator's loop in `DarkFactory/ShiftCoordinator.ps1` removed only lanes asked to retire from `$activeLanes`; an ended lane stayed in it, so the Auto step and the 2-minute ramp counted it as running and never added a lane in its place.

## Acceptance criteria

- [x] The coordinator loop removes every lane that has written its summary from `$activeLanes`, retired or ended, and traces which.
- [x] `RunDarkFactory.ps1 -TestHeartbeat` checks it and passes under Windows PowerShell 5.1 and pwsh 7, and `-TestRestart` still passes.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: e76a30b2 7e496bd7.
- 2026-11-04: Doing -> Done. Ended lanes leave the active set like retired ones; -TestHeartbeat checks it; -TestHeartbeat, -TestRestart and -TestAutoLanes pass under Windows PowerShell 5.1 and pwsh 7.
