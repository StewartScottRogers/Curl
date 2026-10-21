---
id: BL-2927
title: Split Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2927 — Split Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs so tasks touching it can run in parallel

## Goal

`Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 32 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 8 unfinished lane-eligible tasks touching `Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs`: BL-2834, BL-2836, BL-2856, BL-2858, BL-2861, BL-2862, BL-2881, BL-2917. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-2834, BL-2836, BL-2856, BL-2858, BL-2861, BL-2862, BL-2881, BL-2917) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 816a565d 65780150 a0b49a29.
