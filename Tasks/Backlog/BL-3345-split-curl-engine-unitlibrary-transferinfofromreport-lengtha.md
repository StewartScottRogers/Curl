---
id: BL-3345
title: Split Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3345 — Split Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 5 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs`: BL-3293, BL-3301, BL-3302, BL-3338, BL-3339. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-3293, BL-3301, BL-3302, BL-3338, BL-3339) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
