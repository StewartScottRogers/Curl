---
id: BL-4665
title: Split Curl.Networking.UnitLibrary so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4665 — Split Curl.Networking.UnitLibrary so tasks touching it can run in parallel

## Goal

`Curl.Networking.UnitLibrary` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 5 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Networking.UnitLibrary`: BL-4514, BL-4515, BL-4516, BL-4639, BL-4640, BL-4654. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Networking.UnitLibrary` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4514, BL-4515, BL-4516, BL-4639, BL-4640, BL-4654) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Networking.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The library already holds one type per file (214 files), so no code moved. The two waiting tasks that really change it, BL-4640 and BL-4654, now name only their own files in `touches`; BL-4639 is no longer on the board. BL-4514 to BL-4516 name no Networking path (the hotspot report lists them anyway, likely a board quirk); `hotspots` now shows 5 per file including this task, 4 once it is Done.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 30f5b0a2 bd718253 a0b49a29.
- 2026-11-03: Doing -> Done. Library already one type per file; narrowed BL-4640 and BL-4654 touches to their own files
