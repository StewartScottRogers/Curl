---
id: BL-2884
title: Split UpstreamCaseRunner.cs so stand-ins and case setups register themselves in their own files
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2884 — Split UpstreamCaseRunner.cs so stand-ins and case setups register themselves in their own files

## Goal

Each stand-in, debug-variable emulation and per-case setup the runner knows lives in its own file and registers itself, so a task that adds one adds a file instead of editing UpstreamCaseRunner.cs.

## Context

Stewart, 2026-10-21: "always function in a highly parallel fashion". Two tasks whose `touches` overlap never run at once, and Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs is named by the `touches` of these waiting tasks: BL-2832, BL-2835, BL-2837, BL-2850, BL-2851, BL-2854, BL-2855, BL-2859, BL-2860. So they run one at a time while the board has lanes to spare. See CLAUDE.md's "Work runs as parallel as it can" paragraph and the planner's rule on one self-registering file per case.

Move each case- or stand-in-specific branch out of UpstreamCaseRunner.cs into its own type implementing a small interface (or a partial-class file), listed in one registration table that is AOT-safe (no reflection scanning). Its tests move beside it in UpstreamCaseRunnerTests.<Name>.cs.

## Acceptance criteria

- [x] Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs keeps only the shared code; every case-, stand-in- or feature-specific piece is in its own file.
- [x] Adding a new one needs a new file plus at most one line in a registration table, and the table is the only shared edit.
- [x] No behaviour changes: the same upstream cases pass (PassingUpstreamCases.txt unchanged) and the moved tests keep their names.
- [x] The waiting tasks listed in Context have their `touches` narrowed by editing their front matter to the new files they need (or the registration table), so `task-board.ps1 capacity` counts more of them as able to start together.
- [x] `dotnet build` clean and the fast tests green; Curl.Conformance.UnitLibrary stays inside the quality gates.

## Notes

- Design: an abstract `UpstreamStandInFront` per stand-in (variables, features, wrapping at a named stage of the server chain, log-directory files, uploads), registered in `UpstreamStandInFronts.Create`; nine fronts moved out of the runner with no behaviour change (Curl.Conformance.UnitTests all green, PassingUpstreamCases.txt untouched). Library quality run: only the two uncovered base members, since made abstract.
- Waiting tasks narrowed: BL-2832, 2835, 2837, 2859, 2860 now name UpstreamStandInFronts.cs instead of the runner; BL-2850, 2851, 2854 name UpstreamPerlCheckLine.cs (the check-line dispatcher); BL-2855 drops the runner and runner tests. UpstreamCaseRunnerTests is now partial, so new runner tests go in UpstreamCaseRunnerTests.<Name>.cs.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 566bfbec df0009a6 ee115027 1ff6ab0b 38e9dffd.
- 2026-10-21: Doing -> Done. Stand-ins split into self-registering fronts; build clean, fast Conformance tests green
