---
id: BL-807
title: Cap dark factory lanes by free memory and a 4x build slowdown instead of 2x
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-806]
touches: [RunDarkFactory.ps1, Documentation/Planning/Decisions/ADR-0130-lanes-auto-paces-dark-factory-lanes-to-the-usage-windows-the-board-and-the-machine.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-807 — Cap dark factory lanes by free memory and a 4x build slowdown instead of 2x

## Goal

The machine probe passes a step while all its builds succeed, the wall time is at most
4.0 times one build, and at least 20% of memory stays free; a `machine-lanes.json` probed
under another rule is measured again at the next Auto shift.

## Context

Debugged 2026-09-28: this 32-processor, 125.6 GB PC was capped at 3 lanes because four
simultaneous builds took 36.7 s against 13.6 s for one (2.7x) with 71.5% of memory free.
Lanes build for a few minutes of each task and integrate one at a time under
`integrate.lock`, so N simultaneous builds is a worst case that almost never happens, and
2x is too strict a knee for it. Memory is the limit that matters.

## Acceptance criteria

- [x] `$MachineProbeRule` is `wall <= 4.0x one build and free memory >= 20%` and
      `Test-MachineProbeStep` applies exactly that.
- [x] `Get-MachineProbeNeed` returns `the probe rule changed` when the record's `rule`
      differs from `$MachineProbeRule`, so the next Auto shift probes again.
- [x] `-TestMachineProbe` cases cover a time knee past 4x, a memory knee under 20%, a
      failed build, all 16 passing, a cut-short probe and a failing first step; all pass.
- [x] ADR-0130 item 7 records the new rule in a dated amendment.

## Notes

`depends-on` BL-806 only because both edit `RunDarkFactory.ps1`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
