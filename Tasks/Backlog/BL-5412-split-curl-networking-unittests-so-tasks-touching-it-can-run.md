---
id: BL-5412
title: Split Curl.Networking.UnitTests so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5412 — Split Curl.Networking.UnitTests so tasks touching it can run in parallel

## Goal

`Curl.Networking.UnitTests` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 16 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 9 unfinished lane-eligible tasks touching `Curl.Networking.UnitTests`: BL-5375, BL-5382, BL-5384, BL-5385, BL-5388, BL-5393, BL-5394, BL-5395, BL-5396. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Networking.UnitTests` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-5375, BL-5382, BL-5384, BL-5385, BL-5388, BL-5393, BL-5394, BL-5395, BL-5396) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Networking.UnitTests`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
