---
id: BL-5949
title: Split Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5949 — Split Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs so tasks touching it can run in parallel

## Goal

`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 5 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`: BL-5923, BL-5944, BL-5945, BL-5946, BL-5948. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-5923, BL-5944, BL-5945, BL-5946, BL-5948) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The 4522-line file is now one partial class in 31 files (ADR-1391): `HttpProtocolHandler.cs`
  (365 lines: constructor, properties, `ExecuteAsync`) and one `HttpProtocolHandler.<Concern>.cs`
  per concern. A partial file registers itself: the project compiles every `.cs` file.
- Code moved only: the 4117 non-blank lines other than the scaffolding (usings, namespace, class
  line, braces) are the same before and after; only usings and one comment per new file were added.
- `touches` widened to the library folder (new files and its `CLAUDE.md`); no other task in Doing
  named it on `origin/work/dark-factory`.
- Narrowed `touches` of BL-5945, BL-5946, BL-5948, and also BL-5951 and BL-5952 (filed since this
  task), and pointed their Context at the new files. BL-5923 keeps its empty `touches`: it reformats
  the whole solution. BL-5944 is Done and unchanged. `hotspots` then printed `No hotspots.`
- Checks: `dotnet build` clean; fast tests green (HTTP: 2779 passed, 44 skipped);
  `dotnet format --verify-no-changes` reports nothing for the handler files;
  `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: c1030c44 28cae71e a0b49a29.
- 2026-11-25: Doing -> Done. HttpProtocolHandler is split into one partial file per concern, so the HTTP tasks that waited on it can run in parallel
