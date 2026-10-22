---
id: BL-3094
title: Split RunDarkFactory.ps1 so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3094 — Split RunDarkFactory.ps1 so tasks touching it can run in parallel

## Goal

`RunDarkFactory.ps1` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 12 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 14 unfinished lane-eligible tasks touching `RunDarkFactory.ps1`: BL-3065, BL-3066, BL-3079, BL-3080, BL-3081, BL-3082, BL-3083, BL-3084, BL-3085, BL-3086, BL-3087, BL-3088, BL-3089, BL-3092. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `RunDarkFactory.ps1` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3065, BL-3066, BL-3079, BL-3080, BL-3081, BL-3082, BL-3083, BL-3084, BL-3085, BL-3086, BL-3087, BL-3088, BL-3089, BL-3092) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `RunDarkFactory.ps1`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

RunDarkFactory.ps1 was already only help, parameters and a loader; each area's code lives in DarkFactory\<Area>.ps1 (one file per unit, loaded by the area list). So criterion 1 held already; the work was narrowing the waiting tasks' touches to the DarkFactory file each would change (CI red -> CiWatch; requeue/claim -> Requeue, Claim; stash -> TaskStash; rebase/cost-with-resolve -> Integrate; BL-3079 keeps TaskBudget). hotspots now prints 'No hotspots'. BL-3064 and BL-3080 (still RunDarkFactory.ps1) left as they are: only three tasks, under the limit.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: e264aee4 b2b166e5 a0b49a29.
- 2026-10-22: Doing -> Done. waiting tasks' touches narrowed to DarkFactory area files; hotspots empty
