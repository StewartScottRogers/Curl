---
id: BL-2883
title: Split UpstreamConformanceTests.cs so the upstream-case tasks that add tests to it can run in parallel
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2883 — Split UpstreamConformanceTests.cs so the upstream-case tasks that add tests to it can run in parallel

## Goal

Each upstream-case test group in UpstreamConformanceTests.cs lives in its own file, so a task that adds tests for its cases adds a new file instead of editing the shared one.

## Context

Stewart, 2026-10-21: "always function in a highly parallel fashion". Two tasks whose `touches` overlap never run at once, and Curl.Conformance.UnitTests/UpstreamConformanceTests.cs is named by the `touches` of these waiting tasks: BL-2832, BL-2833, BL-2834, BL-2835, BL-2836, BL-2837, BL-2838, BL-2839, BL-2850, BL-2856, BL-2859, BL-2860. So they run one at a time while the board has lanes to spare. See CLAUDE.md's "Work runs as parallel as it can" paragraph and the planner's rule on one self-registering file per case.

Make UpstreamConformanceTests a partial class (or a set of small test classes sharing a fixture base) with one file per case group, e.g. UpstreamConformanceTests.<Group>.cs; leave in the original file only the shared fixture, helpers and the ratchet test.

## Acceptance criteria

- [ ] Curl.Conformance.UnitTests/UpstreamConformanceTests.cs keeps only the shared code; every case-, stand-in- or feature-specific piece is in its own file.
- [ ] Adding a new one needs a new file plus at most one line in a registration table, and the table is the only shared edit.
- [ ] No behaviour changes: the same upstream cases pass (PassingUpstreamCases.txt unchanged) and the moved tests keep their names.
- [ ] The waiting tasks listed in Context have their `touches` narrowed by editing their front matter to the new files they need (or the registration table), so `task-board.ps1 capacity` counts more of them as able to start together.
- [ ] `dotnet build` clean and the fast tests green; Curl.Conformance.UnitLibrary stays inside the quality gates.

## Notes

## Log

- 2026-10-21: Created.
