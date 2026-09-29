---
id: BL-806
title: Stop -Lanes Auto at the weekly limit instead of pacing to it, and wait quietly for the weekly reset
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, CLAUDE.md, Documentation/Planning/Decisions/ADR-0130-lanes-auto-paces-dark-factory-lanes-to-the-usage-windows-the-board-and-the-machine.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-806 — Stop -Lanes Auto at the weekly limit instead of pacing to it, and wait quietly for the weekly reset

## Goal

By default `-Lanes Auto` sizes lanes to the 5-hour window, the board and the machine
only, stops claiming at `-StopAtWeeklyUsage` (97%), and a shift that finds the weekly
window used up waits for its reset with a notice instead of the alarm.

## Context

Debugged 2026-09-28 with Stewart: at 18:00 the weekly pace retired lane 3 (13% of the
week used, reset Wed 06:00, 0.885 points per lane-hour: (97 - 13) / (0.885 x 36) = 2.6).
Pacing the weekly window cannot finish more work than running until 97% and then waiting:
the budget spent by the reset is the same, and pacing loses budget whenever the factory
idles, because what is left at the reset is gone. ADR-0130 made weekly pacing the default.
This task reverses that default (decided by Claude under Stewart's delegation, with his
"go"), keeping pacing as an opt-in `-WeeklyPace`.

## Acceptance criteria

- [x] `-NoWeeklyPace` is replaced by `-WeeklyPace`; without it `Get-AutoLaneStep` passes
      `-WeeklyPace $false`, and `Get-PaceTarget` defaults to `$false`. `-Continuous`
      hands `-WeeklyPace` on; `-AutoLanesReport` says "(not pacing weekly)" without it.
- [x] `Wait-ForFreshSession` no longer returns an alarm reason for a used-up weekly window:
      it traces the reset time, waits for it with `Wait-ForNewSession -UsageOnly`, then
      checks the 5-hour window as today.
- [x] `-TestAutoLanes` passes, with the weekly-pacing cases passing `-WeeklyPace` explicitly.
- [x] ADR-0130 has a dated amendment; the script header and `CLAUDE.md` no longer say a
      used-up weekly window raises the alarm.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
