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
completed: 2026-10-21
---
# BL-2927 — Split Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs so tasks touching it can run in parallel

## Goal

`Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 32 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 8 unfinished lane-eligible tasks touching `Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs`: BL-2834, BL-2836, BL-2856, BL-2858, BL-2861, BL-2862, BL-2881, BL-2917. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-2834, BL-2836, BL-2856, BL-2858, BL-2861, BL-2862, BL-2881, BL-2917) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Conformance.UnitLibrary/UpstreamPlatformFeatureTable.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- The code split was already done by BL-2914: each feature list is its own Upstream*Features.cs, one name per line, and the table holds one line per list, all merge=union and listed in Tasks/SharedListFiles.txt. The board reading that file is the guard pull request #184 (guard/shared-list-files, open, interactive only), so until it merges the table still counted as a hotspot.
- Decision (default taken): narrow each waiting task's touches to the list file its feature belongs in, since adding a name to an existing list needs no table change. BL-2856 ldap -> UpstreamServedProtocolFeatures.cs; BL-2858 Unicode/ssl-sessions -> UpstreamWindowsFeatures.cs and UpstreamUnixFeatures.cs; BL-2861 codeset-utf8 -> UpstreamHarnessFeatures.cs; BL-2862 --libcurl -> UpstreamToolFeatures.cs. BL-2834 and BL-2836 report Debug per case from their own UpstreamConformanceTests.*StandIn.cs, so the table was dropped from their touches. BL-2881 touches the whole Curl.Conformance.UnitLibrary project and was left as it is; BL-2917 is Done.
- Measured: `task-board.ps1 hotspots` no longer lists UpstreamPlatformFeatureTable.cs (at most BL-2881 covers it). dotnet build clean; Curl.Conformance.UnitTests fast tests 3746 passed, 0 failed (no code changed, so only the affected project was run).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 816a565d 65780150 a0b49a29.
- 2026-10-21: Doing -> Done. Waiting tasks narrowed to their own feature-list files; the table is no longer a hotspot
