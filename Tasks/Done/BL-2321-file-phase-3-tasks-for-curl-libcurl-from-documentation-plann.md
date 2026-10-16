---
id: BL-2321
title: File phase 3 tasks for Libcurl from Documentation/Planning/Curl-Api-And-libcurl.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2319, BL-2320]
touches: [Tasks/Backlog, Documentation/Planning/Curl-Api-And-libcurl.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2321 — File phase 3 tasks for Libcurl from Documentation/Planning/Curl-Api-And-libcurl.md

## Goal

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` - `Libcurl`, area by area - is on the board as tasks sized for one `/task-run` each, with dependencies, honest `touches` and checkable acceptance criteria, so the board keeps itself going.

## Context

ADR-0506; the plan's "Phase 3" table (about 55-60 tasks). Phases 1 and 2 decided the seam's real shape; read the code as it is now, not the plan's guesses, and correct the plan where they differ. Use the task-board skill (`task-board.ps1 new` only) or the `task-planner` agent. Rules that carry over:
- Every task that changes behaviour keeps the ratchet (`Curl.Conformance.UnitTests/PassingUpstreamCases.txt`) unchanged or growing, and states build, fast tests and `Measure-CodeQuality.ps1` in its criteria.
- Use sub-folder `touches` in `Libcurl` (one folder per area: `Easy`, `Options`, `Info`, `Callbacks`, `Multi`, `Share`, `Url`, `Mime`, `WebSocket`, `Headers`, `Misc`) so areas run in parallel lanes.
- The `CurlEasyErrorText` move is two tasks: first an interactive-only one (`-NoLane`, `touches` naming `Gap/Tools/Measure-ExitCodeGap.ps1` and `Gap/Instructions/ExitCodes.md`) that makes the gap office's exit-code measurement find the table in its new home, done on the `gap` branch; then the lane task that moves it, depending on the first.
- The last phase 3 task files phase 4 (`Curl.Api`) the same way.

## Acceptance criteria

- [x] The board holds a task for every row of the plan's phase 3 table (or the plan's table is corrected with the reason), each with non-empty `touches`, a `depends-on` chain and acceptance criteria naming build, fast tests, quality gates and the ratchet.
- [x] `/task-check` (or `task-board.ps1 status`) reports no dependency cycle and no duplicate ID.
- [x] The plan's "Filed tasks" section lists the phase 3 task IDs.
- [x] A final phase 3 task "File phase 4 tasks for Curl.Api from Documentation/Planning/Curl-Api-And-libcurl.md" exists, depending on the phase 3 easy and multi tasks.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Phase 3 filed as BL-2395 to BL-2455 (61 tasks) and the plan lists them
