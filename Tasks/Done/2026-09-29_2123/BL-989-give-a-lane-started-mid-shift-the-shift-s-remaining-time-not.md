---
id: BL-989
title: Give a lane started mid-shift the shift's remaining time, not a fresh -Hours, so it stops claiming when the shift's time is up
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-989 — Give a lane started mid-shift the shift's remaining time, not a fresh -Hours, so it stops claiming when the shift's time is up

## Goal

Every lane of a shift stops claiming new tasks at the shift's end, however late in the shift `-Lanes Auto` started or restarted it.

## Context

Shift 20260929-084219 ran until 16:42. `-Lanes Auto` retired lane 6 at 11:00 (`lanes 6 -> 5`) and started it again at 11:30. The coordinator's `$laneArgsFor` passes `-Hours $Hours`, so the new lane process computed its own end as 11:30 + 8 h = 19:30. At 16:42 the coordinator logged `shift time up` and the other lanes finished, but lane 6 kept claiming (BL-681 at 18:15), holding the whole shift, its merge to `master` and the `-Continuous` next shift, with five lanes idle.

## Acceptance criteria

- [x] `$laneArgsFor` passes the time left until the coordinator's `$shiftEnd` as `-Hours`, computed when the lane starts, so an added or restarted lane ends with the shift.
- [x] `RunDarkFactory.ps1` still parses (`[System.Management.Automation.Language.Parser]::ParseFile` reports no errors).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Lanes added or restarted mid-shift now get the shift's remaining time as -Hours and stop with it.
