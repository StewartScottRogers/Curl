---
id: BL-3243
title: Split Documentation/Planning/Decisions/README.md so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3243 — Split Documentation/Planning/Decisions/README.md so tasks touching it can run in parallel

## Goal

`Documentation/Planning/Decisions/README.md` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 9 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Documentation/Planning/Decisions/README.md`: BL-3171, BL-3207, BL-3217, BL-3221, BL-3231, BL-3233. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Documentation/Planning/Decisions/README.md` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-3171, BL-3207, BL-3217, BL-3221, BL-3231, BL-3233) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Documentation/Planning/Decisions/README.md`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decided: the index table is one line per ADR, so it is a shared list file (Tasks/SharedListFiles.txt, .gitattributes merge=union). Each ADR file is its own unit and its README row is its registration; adding one touches no other task's file. Removed the README from BL-3171, BL-3207, BL-3217, BL-3233 touches (BL-3231 is Done; BL-3221 names the whole Documentation folder, unchanged). hotspots now lists no path for the README.
- Touched Tasks/SharedListFiles.txt and .gitattributes (the mechanism; no other Doing task names them).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ba3fb9f3 8fb7231c a0b49a29.
- 2026-10-23: Doing -> Done. Decisions README is a shared list file; waiting tasks' touches narrowed
