---
id: BL-3330
title: Split Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3330 — Split Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 10 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs`: BL-3293, BL-3301, BL-3302, BL-3328, BL-3329. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3293, BL-3301, BL-3302, BL-3328, BL-3329) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split ConnectionEndPointRecorder into partial files: .EndPoints.cs, .OsErrno.cs, .EarlierHop.cs (fields live with their unit; Clear and ReportOn compose them). Of the waiting tasks only BL-3328 named the file; its touches now name .OsErrno.cs. BL-3293, BL-3301 and BL-3302 name the whole library, not this file. Hotspots no longer lists the file. Build clean, 2434 engine tests pass, quality run 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 63ea8d75 b96be207 a0b49a29.
- 2026-10-23: Doing -> Done. ConnectionEndPointRecorder split into one partial file per unit; BL-3328 touches narrowed
