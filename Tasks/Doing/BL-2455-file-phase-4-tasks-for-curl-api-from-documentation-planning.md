---
id: BL-2455
title: File phase 4 tasks for Curl.Api from Documentation/Planning/Curl-Api-And-libcurl.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2408, BL-2429, BL-2454]
touches: [Tasks/Backlog, Documentation/Planning/Curl-Api-And-libcurl.md]
requirement: none
created: 2026-10-15
completed:
---
# BL-2455 — File phase 4 tasks for Curl.Api from Documentation/Planning/Curl-Api-And-libcurl.md

## Goal

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` - `Curl.Api`, the .NET-normal API - is on the board as tasks sized for one `/task-run` each, with dependencies, honest `touches` and checkable acceptance criteria, as this task filed phase 3. The last phase 4 task files phase 5 the same way.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] The board holds a task for every phase 4 bullet (or the plan is corrected with the reason), each with non-empty `touches`, a `depends-on` chain and criteria naming build, fast tests, quality gates and the ratchet; `task-board.ps1 status` reports no cycle or duplicate; the plan's "Filed tasks" section lists them.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
