---
id: BL-823
title: Start -Lanes Auto at the ceiling and retire straight down to a confirmed low pace
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Planning/Decisions/ADR-0130-lanes-auto-paces-dark-factory-lanes-to-the-usage-windows-the-board-and-the-machine.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-823 — Start -Lanes Auto at the ceiling and retire straight down to a confirmed low pace

## Goal

A `-Lanes Auto` shift starts at its ceiling (board capacity, machine cap, `-MaxLanes`)
and, when a step finds the count too high, retires straight down to the count the pace
or ceiling allows instead of one lane per step.

## Context

Stewart, 2026-09-28: "Just start aggressively and dial down", and "go" to making it the
default. With BL-808's two-low-steps rule a shift retires at most one lane every 30
minutes, so from 10 lanes to 4 takes three hours and can reach the 5-hour window's 85%
stop first. Adding lanes stays one per step: the meter needs samples at each count.

## Acceptance criteria

- [ ] `-MinStartLanes` defaults to 16, so an Auto shift starts at its ceiling; its help
      says so. `-MinStartLanes 3` still starts at `max(saved, 3)` as before.
- [ ] `-MaxLanes` defaults to 6 (Stewart, 2026-09-28: "Maybe we should set the max lanes
      to 6"), so starting at the ceiling means at most 6.
- [ ] `Get-NextLaneCount` scales down to `max(1, floor(desired + 0.25))` in one step: at
      once for a ceiling, and for a pace target once `-PreviousLow` confirms it. Up stays
      one lane per step.
- [ ] The coordinator asks that many lanes to retire in one step, highest numbers first,
      and traces each.
- [ ] `-TestAutoLanes` covers a multi-lane pace drop, a multi-lane ceiling drop and the
      unchanged single steps, and passes.
- [ ] ADR-0130 has a dated amendment; the script header says how the start and the drop
      work.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Handed to the dark factory: a task in Doing that no lane holds stops a shift from starting.
