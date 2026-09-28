---
id: BL-780
title: Start a -Lanes Auto shift at no fewer than -MinStartLanes lanes, 3 by default
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Planning/Decisions/ADR-0130-lanes-auto-paces-dark-factory-lanes-to-the-usage-windows-the-board-and-the-machine.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-780 — Start a -Lanes Auto shift at no fewer than -MinStartLanes lanes, 3 by default

## Goal

A `-Lanes Auto` shift starts at the larger of the lane count `auto-lanes.json` saved and
`-MinStartLanes` (default 3), still capped by the board's capacity, the machine cap and
`-MaxLanes`, and from then on Auto steps up or down exactly as it does today.

## Context

Stewart, 2026-09-28: a shift should start with at least three lanes. He chose a starting
count, not a hard floor: Auto must still be free to retire lanes below 3 when the burn
rate says so, because a fixed floor ran him out of tokens on Max 5X (see ADR-0130).

Today `Get-AutoStartCount` in `RunDarkFactory.ps1` (around line 1984) starts at the saved
count, or 2 on the first Auto shift. Only the start changes; `Get-NextLaneCount` and the
15-minute step are untouched.

## Acceptance criteria

- [x] `RunDarkFactory.ps1` has `[ValidateRange(1, 16)][int]$MinStartLanes = 3`, documented
      in the header's AUTO LANES section beside `-MaxLanes`.
- [x] `Get-AutoStartCount` returns max(saved count or 3 on a first shift, `-MinStartLanes`),
      then applies the existing ceiling cap; its `Why` says when `-MinStartLanes` raised the
      count, e.g. `last shift saved 2, raised to 3 by -MinStartLanes`.
- [x] The script's own self-check cases cover: saved 2 -> 3; saved 5 -> 5; no saved file
      -> 3; `-MinStartLanes 4` with a ceiling of 2 -> 2; `-MinStartLanes 1` with saved 1 -> 1.
      Running them passes.
- [x] `-AutoLanesReport` prints the start count and its reason including the new rule.
- [x] ADR-0130 gains a dated amendment recording the start rule and that it is not a floor.
- [x] `-Lanes N` (a fixed count) ignores `-MinStartLanes`.

## Notes

Pipeline `direct`: a script change, then the `verify` skill. `touches` lists the script
and ADR-0130 only; the running shift reads the script at start, so the change takes effect
from the next shift.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
