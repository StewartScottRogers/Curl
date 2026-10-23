---
id: BL-3202
title: Split Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3202 — Split Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 7 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs`: BL-3193, BL-3195, BL-3196, BL-3199, BL-3201. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3193, BL-3195, BL-3196, BL-3199, BL-3201) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 91373e82 c92c6032 a0b49a29.
- 2026-10-22: Doing -> Done. ManagedEasyTransfer split into per-concern partial files; hotspot cleared
