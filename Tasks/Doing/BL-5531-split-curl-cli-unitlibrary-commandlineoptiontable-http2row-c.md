---
id: BL-5531
title: Split Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5531 — Split Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs so tasks touching it can run in parallel

## Goal

`Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 1 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs`: BL-3358, BL-3431, BL-3432, BL-3569, BL-5006. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3358, BL-3431, BL-3432, BL-3569, BL-5006) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Cli.UnitLibrary/CommandLineOptionTable.Http2Row.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The file already held one unit (the `--http2` row, split by BL-5529), so there was nothing left to split. BL-5006 named the whole Curl.Cli.UnitLibrary though it only needs CurlOptionAliasTable.cs; its touches are narrowed to that file. The hotspot now lists BL-3358, BL-3431, BL-3432, BL-3569 (they change this row) and BL-5531 itself, which leaves it on Done: 4 tasks, fewer than 5. No code changed, so no build or tests were needed.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 478ec796 684cb54e a0b49a29.
