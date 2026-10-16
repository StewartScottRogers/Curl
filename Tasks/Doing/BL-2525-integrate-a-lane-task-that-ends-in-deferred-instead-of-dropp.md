---
id: BL-2525
title: Integrate a lane task that ends in Deferred instead of dropping it as a stall
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-16
completed:
---
# BL-2525 — Integrate a lane task that ends in Deferred instead of dropping it as a stall

## Goal

When a lane's /task-run moves its task to `Deferred`, the move and the lane's commit reach `work/dark-factory`, and the trace reports the task as deferred rather than STALLED.

## Context

Shift 20261016-081911, lane 3: BL-2496 (now BL-2523) was claimed at 11:30, measured correctly that curl 8.22.0 already refuses `--proto =ipfs,http`, moved itself to Deferred and committed. The trace line was `11:33:01 L3 BL-2496 STALL ended in Deferred, exit 0`. The lane's Deferred move was never integrated, so the task sat in `Doing` on the shared branch with no lane holding it, for about 9 hours, until an interactive session applied the move by hand.

Cause: the outcome chain in `RunDarkFactory.ps1` (around line 5395) handles `Done`, `Blocked`, and `Backlog`/`Parked`; every other state, `Deferred` included, goes to the stall branch, which neither integrates the lane's commits nor counts it as a valid outcome, and adds to `$failStreak`.

`Deferred` is a valid /task-run outcome (SKILL.md), so a lane must treat it like `Blocked`: integrate and push the board change, reset the fail streak, and report it.

## Acceptance criteria

- [ ] A lane run that ends with its task in `Deferred` integrates the lane's commits into `work/dark-factory` the same way a `Blocked` outcome does, so the task file is in `Tasks/Deferred` on the shared branch.
- [ ] The trace writes a `DEFERRED` line with the task's last Log line, and the shift-end summary counts it, not a `STALL`.
- [ ] A `Deferred` outcome does not add to the run fail streak.
- [ ] Any other unexpected end state (for example `Doing`) still reports STALLED as now.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 321353a6 07c55dd5 f6e836bb 04286d61 3d836d5e.
