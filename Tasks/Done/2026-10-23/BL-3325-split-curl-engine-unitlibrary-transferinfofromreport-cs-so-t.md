---
id: BL-3325
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
# BL-3325 — Split Curl.Engine.UnitLibrary/TransferInfoFromReport.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 7 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 8 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`: BL-3293, BL-3301, BL-3302, BL-3315, BL-3317, BL-3320, BL-3321, BL-3323. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3293, BL-3301, BL-3302, BL-3315, BL-3317, BL-3320, BL-3321, BL-3323) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Split the RTSP, OS errno and proxy error units into TransferInfoFromReport.Rtsp.cs, .OsErrno.cs and .ProxyError.cs beside the existing Auth, CertInfo, LengthAndCondition, RetryAfter and Scheme partials; the main file keeps only the composition.
- Narrowed touches of BL-3315 (OsErrno.cs), BL-3317 (Scheme.cs), BL-3323 (dropped the main file), BL-3320 and BL-3321 (Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, where header size and response code come from). `hotspots` no longer lists TransferInfoFromReport.cs. BL-3293, BL-3301 and BL-3302 touch the whole Engine library and are left for their own runs.
- Engine tests 2422 passed; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: da45f4f5 02f67491 a0b49a29.
- 2026-10-23: Doing -> Done. TransferInfoFromReport.cs split into one partial per unit; waiting tasks narrowed
