---
id: BL-2514
title: File phase 5 tasks: port tests/libtest and measure libcurl-api
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2513]
touches: [Tasks/Backlog, Documentation/Planning/Curl-Api-And-libcurl.md]
requirement: none
created: 2026-10-16
completed:
---
# BL-2514 — File phase 5 tasks: port tests/libtest and measure libcurl-api

## Goal

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` is on the board as tasks sized for one `/task-run` each, with dependencies, honest `touches` and checkable acceptance criteria, as phases 3 and 4 were.

## Context

Phase 5 section of the plan; ADR-0506. Read `Libcurl` as it is, the upstream case runner in the conformance projects and the 362 `libcurl-api` programs in the targeted tarball (`Gap/Baselines/target.json`), and correct the plan where they differ. The gap office part (`Gap/`) is interactive only: file it with `-NoLane`.

## Acceptance criteria

- [ ] The board holds a task for every phase 5 bullet (or the plan is corrected with the reason), each with non-empty `touches`, a `depends-on` chain and criteria naming build, fast tests, quality gates and the ratchet; `task-board.ps1 status` reports no cycle or duplicate; the plan "Filed tasks" section lists them.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 1f588d3c 4272a6b9 8738c7f6.
