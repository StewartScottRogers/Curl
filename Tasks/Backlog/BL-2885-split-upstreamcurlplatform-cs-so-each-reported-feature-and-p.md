---
id: BL-2885
title: Split UpstreamCurlPlatform.cs so each reported feature and platform answer lives in its own file
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2885 — Split UpstreamCurlPlatform.cs so each reported feature and platform answer lives in its own file

## Goal

Each feature and platform answer UpstreamCurlPlatform reports to the upstream harness lives in its own file, so a task that reports a new feature adds a file instead of editing UpstreamCurlPlatform.cs.

## Context

Stewart, 2026-10-21: "always function in a highly parallel fashion". Two tasks whose `touches` overlap never run at once, and Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs is named by the `touches` of these waiting tasks: BL-2834, BL-2836, BL-2838, BL-2839, BL-2856, BL-2858, BL-2861, BL-2862. So they run one at a time while the board has lanes to spare. See CLAUDE.md's "Work runs as parallel as it can" paragraph and the planner's rule on one self-registering file per case.

Turn the feature list into entries in one file each (partial class or small provider types collected by an explicit table, no reflection), keeping UpstreamCurlPlatform.cs to the lookup itself. Its tests split the same way.

## Acceptance criteria

- [ ] Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs keeps only the shared code; every case-, stand-in- or feature-specific piece is in its own file.
- [ ] Adding a new one needs a new file plus at most one line in a registration table, and the table is the only shared edit.
- [ ] No behaviour changes: the same upstream cases pass (PassingUpstreamCases.txt unchanged) and the moved tests keep their names.
- [ ] The waiting tasks listed in Context have their `touches` narrowed with `task-board.ps1 edit` to the new files they need (or the registration table), so `task-board.ps1 capacity` counts more of them as able to start together.
- [ ] `dotnet build` clean and the fast tests green; Curl.Conformance.UnitLibrary stays inside the quality gates.

## Notes

## Log

- 2026-10-21: Created.
