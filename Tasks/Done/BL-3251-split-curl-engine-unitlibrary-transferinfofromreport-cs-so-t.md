---
id: BL-3251
title: Split Curl.Engine.UnitLibrary/TransferInfoFromReport.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3251 — Split Curl.Engine.UnitLibrary/TransferInfoFromReport.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 8 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`: BL-3193, BL-3194, BL-3218, BL-3229, BL-3230. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3193, BL-3194, BL-3218, BL-3229, BL-3230) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split into partial files by concern: `.Auth.cs`, `.CertInfo.cs`, `.LengthAndCondition.cs`, `.RetryAfter.cs`, `.Scheme.cs`; the core file keeps `Of` and `WithCounts`. BL-3193 now touches `.Auth.cs`, BL-3194 `.LengthAndCondition.cs`; BL-3218 narrowed to `ManagedCurlUrlHandle.cs`, BL-3229 and BL-3230 to `TransferCredentialLookup.cs`. `task-board.ps1 hotspots` reports none. Build clean, fast Engine tests green, Measure-CodeQuality for Curl.Engine.UnitLibrary 0 failing. No ADR: a mechanical file split.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: da45f4f5 e29fc199 a0b49a29.
- 2026-10-23: Doing -> Done. TransferInfoFromReport split into per-concern partial files; waiting tasks' touches narrowed
