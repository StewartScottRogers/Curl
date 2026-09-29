---
id: BL-812
title: Cap dark factory lanes by free memory alone, dropping the build slowdown test
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Planning/Decisions/ADR-0130-lanes-auto-paces-dark-factory-lanes-to-the-usage-windows-the-board-and-the-machine.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-812 — Cap dark factory lanes by free memory alone, dropping the build slowdown test

## Goal

The machine probe passes a step while every build succeeds and at least 20% of memory
stays free, whatever the builds' wall time; a `machine-lanes.json` probed under another
rule is measured again at the next Auto shift.

## Context

BL-807 relaxed the knee from 2x to 4x and the cap stayed at 3. The 19:18 probe on
2026-09-28 measured one build at 9.0 s, so four took 41.4 s = 4.60x, with 63.8% of memory
still free. A slowdown ratio swings with the one-build baseline (13.6 s in the first
probe, 9.0 s in this one) and models lanes building at the same moment, which they rarely
do: a lane builds for minutes of a task that runs most of an hour, and lanes integrate
one at a time. Stewart said "cap by memory".

## Acceptance criteria

- [x] `$MachineProbeRule` is `every build succeeds and free memory >= 20%`, and
      `Test-MachineProbeStep` checks exactly that; `seconds` and `slowdown` are still
      recorded per step for reading, but decide nothing.
- [x] `-TestMachineProbe` covers a memory knee, a slow step that still passes, a failed
      build, all 16 passing, a cut-short probe and a failing first step; all pass.
- [x] The script header and ADR-0130 item 7 state the new rule, with a dated amendment.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Probe passes a step on successful builds and >= 20% free memory; ADR-0130 amended; all self-checks, build and fast tests green.
