---
id: BL-808
title: Meter the 5-hour burn over 60 minutes and retire a lane only after two low steps in a row
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-807]
touches: [RunDarkFactory.ps1, Documentation/Planning/Decisions/ADR-0130-lanes-auto-paces-dark-factory-lanes-to-the-usage-windows-the-board-and-the-machine.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-808 — Meter the 5-hour burn over 60 minutes and retire a lane only after two low steps in a row

## Goal

`-Lanes Auto` stops retiring lanes on rounding noise: the 5-hour burn rate is measured
over up to 60 minutes (at least 30), and a pace target below the band retires a lane only
when the previous step was below the band too.

## Context

Debugged 2026-09-28: usage is reported in whole percentage points, so a 30-minute 5-hour
rate rests on about 5 points and one point of rounding moves the target by about 20%,
while a single step 0.25 lanes low retires a lane. Ceilings (board capacity, machine cap,
`-MaxLanes`) still retire at once: they are exact, not measured.

## Acceptance criteria

- [x] `Get-BurnRate -Window FiveHour` uses a 60-minute window and a 30-minute minimum span.
- [x] `Get-NextLaneCount` takes `-PreviousLow`; a pace-bound target below the band holds
      with `(…, low once)` in the reason when `-PreviousLow` is false, and retires one lane
      when it is true. It returns `Low` so the coordinator can pass it to the next step.
- [x] A ceiling below the current count still retires at once.
- [x] `-TestAutoLanes` covers low once, low twice and the ceiling case, and all cases pass.
- [x] ADR-0130 has a dated amendment for the window and the two-step rule.

## Notes

`depends-on` BL-807 only because both edit `RunDarkFactory.ps1`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. 5-hour rate over 60 min (30 min minimum), pace-bound retire needs two low steps, ceilings retire at once; ADR-0130 amended; -TestAutoLanes 29/29, build and fast tests green.
