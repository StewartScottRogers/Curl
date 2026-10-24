---
id: BL-3433
title: Split Curl.Cli.UnitLibrary so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3433 — Split Curl.Cli.UnitLibrary so tasks touching it can run in parallel

## Goal

`Curl.Cli.UnitLibrary` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 3 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Cli.UnitLibrary`: BL-3358, BL-3359, BL-3430, BL-3431, BL-3432. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Cli.UnitLibrary` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3358, BL-3359, BL-3430, BL-3431, BL-3432) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Cli.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The library was already one file per type; the shared hotspots were CommandLineOptionTable.cs (one array of rows) and CurlVersionText.cs. Moved the HTTP-version rows (http1.0, http1.1, http2, http2-prior-knowledge) into CommandLineOptionTable.HttpVersionRows.cs, spread into the table by a partial class; rows now sit after ipfs-gateway (http2 rows moved up beside http1.1; no test depended on order). Other row groups can be split out the same way when a task needs them.
- Narrowed BL-3358, BL-3430, BL-3431, BL-3432 touches to their own files; hotspots no longer lists Curl.Cli.UnitLibrary. Measure-CodeQuality for Curl.Cli.UnitLibrary: 0 failing members. Fast tests green.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b3e5b293 39de07c5 a0b49a29.
- 2026-10-23: Doing -> Done. HTTP-version option rows in their own file; waiting tasks narrowed, Cli.UnitLibrary no longer a hotspot
