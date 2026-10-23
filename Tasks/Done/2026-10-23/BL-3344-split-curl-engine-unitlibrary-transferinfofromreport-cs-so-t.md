---
id: BL-3344
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
# BL-3344 — Split Curl.Engine.UnitLibrary/TransferInfoFromReport.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 5 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`: BL-3293, BL-3301, BL-3302, BL-3337, BL-3338, BL-3339. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3293, BL-3301, BL-3302, BL-3337, BL-3338, BL-3339) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The file was already split by BL-3251 into eight kind-per-file partials (Auth, CertInfo, LengthAndCondition, OsErrno, ProxyError, RetryAfter, Rtsp, Scheme); what remains in TransferInfoFromReport.cs is only the 12-line composition (Of and WithCounts), which a new kind adds one call to. Narrowed BL-3338 and BL-3339 touches from the hub file to TransferInfoFromReport.LengthAndCondition.cs. hotspots no longer lists TransferInfoFromReport.cs (3 whole-library tasks cover it). No code changed, so no build or quality run was needed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: da45f4f5 413a72ab a0b49a29.
- 2026-10-23: Doing -> Done. Hub file already split; narrowed BL-3338 and BL-3339 touches
