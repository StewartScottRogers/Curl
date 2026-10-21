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
completed: 2026-10-21
---
# BL-2994 — Split Curl.Conformance.UnitTests/UpstreamConformanceTests.cs so tasks touching it can run in parallel

## Goal

`Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 8 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`: BL-2834, BL-2856, BL-2881, BL-2991, BL-2992, BL-2993. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-2834, BL-2856, BL-2881, BL-2991, BL-2992, BL-2993) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The unit that tasks kept adding to was the Perl command script dictionary (BL-2991, BL-2992, BL-2993 each add scripts). It moved to `UpstreamPerlCommandScripts` (registry, `UpstreamPerlCommandScripts.cs`), with one `UpstreamPerlCommandScripts.Test<N>.cs` per script registering itself through `[ModuleInitializer]` (971, 1486, 1707 today). Chosen over reflection scanning: no reflection, no central list; test projects are exempt from AOT anyway. The 1707 registration reads the data folder lazily so module load does not run `UpstreamConformanceTests`' static initializer.
- BL-2834 and BL-2856 still touch `UpstreamConformanceTests.cs`: they change `RunCurlAsync`/`CreateEngine` composition arguments, which is one call, not a list of units. BL-2881 names the whole project. Hotspot count for the file: 2 tasks by name (plus BL-2881), under 5; `task-board.ps1 hotspots` reports "No hotspots."
- Verified: build clean, fast suite 0 failed; cases 971, 1486 and 1707 (listed) still pass.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: cf73cc93 e1a0da91 a0b49a29.
- 2026-10-21: Doing -> Done. Perl command scripts register from one file each; BL-2991/2992/2993 no longer touch UpstreamConformanceTests.cs
