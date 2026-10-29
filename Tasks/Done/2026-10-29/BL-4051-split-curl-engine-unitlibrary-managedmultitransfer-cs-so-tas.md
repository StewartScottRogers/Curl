---
id: BL-4051
title: Split Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4051 — Split Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 19 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 9 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs`: BL-3727, BL-3740, BL-3741, BL-3783, BL-3784, BL-3785, BL-3815, BL-3977, BL-3990. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3727, BL-3740, BL-3741, BL-3783, BL-3784, BL-3785, BL-3815, BL-3977, BL-3990) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split by concern into partial-class files ManagedMultiTransfer.{Entries,Perform,Dispose,WaitSockets,Info,Queueing,MovingOn,Timer,Start,Waiting,MultiEntry}.cs; no logic changed. Narrowed touches of BL-3727, 3740, 3741, 3783-3785, 3815, 3977, 3990 to the files their code lives in (best match by grep; a task needing another file adds it under the touches rule). Hotspots now report none for ManagedMultiTransfer.cs. Engine quality gate: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 3043325a 73dcc4ad a0b49a29.
- 2026-10-28: Doing -> Done. ManagedMultiTransfer split into partial files; touches narrowed; build, tests and quality green
