---
id: BL-2994
title: Split Curl.Conformance.UnitTests/UpstreamConformanceTests.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2994 — Split Curl.Conformance.UnitTests/UpstreamConformanceTests.cs so tasks touching it can run in parallel

## Goal

`Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 8 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`: BL-2834, BL-2856, BL-2881, BL-2991, BL-2992, BL-2993. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [ ] The per-case or per-stand-in content of `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [ ] After the waiting tasks' `touches` (BL-2834, BL-2856, BL-2881, BL-2991, BL-2992, BL-2993) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: cf73cc93 e1a0da91 a0b49a29.
