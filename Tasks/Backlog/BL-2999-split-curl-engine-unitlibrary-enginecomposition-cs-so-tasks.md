---
id: BL-2999
title: Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2999 — Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/EngineComposition.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 9 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/EngineComposition.cs`: BL-2834, BL-2979, BL-2995, BL-2996, BL-2998. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/EngineComposition.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-2834, BL-2979, BL-2995, BL-2996, BL-2998) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/EngineComposition.cs`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
