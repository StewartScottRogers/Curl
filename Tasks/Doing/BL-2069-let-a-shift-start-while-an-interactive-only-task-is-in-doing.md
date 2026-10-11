---
id: BL-2069
title: Let a shift start while an interactive-only task is in Doing
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-11
completed:
---
# BL-2069 — Let a shift start while an interactive-only task is in Doing

## Goal

A shift starts while an interactive session holds an interactive-only task (`lane: no`) in Doing.

## Context

On 2026-10-11 at 08:03 the shift refused to start: "BL-2013 is in Doing and held by no lane, no lane worktree has uncommitted work and no parked branch". An interactive session held BL-2013 (`lane: no`). No lane ever claims such a task, so one in Doing is never a lane's orphan. While interactive gap and audit tasks are worked, that refusal would hold up every lane.

## Acceptance criteria

- [x] Both shift-start checks in `RunDarkFactory.ps1` (the lane shift's orphan settling and the single-lane shift's "task already in Doing") skip a task whose file says `lane: no`, through `Test-InteractiveOnlyTask`.
- [x] `RunDarkFactory.ps1` parses with no errors, and `Test-InteractiveOnlyTask`'s pattern matches BL-2013 (`lane: no`) and not BL-2061.
- [ ] A shift started with BL-2013 in Doing claims lane tasks instead of refusing.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
