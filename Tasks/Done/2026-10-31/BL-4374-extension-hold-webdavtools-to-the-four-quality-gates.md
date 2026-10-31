---
id: BL-4374
title: Extension: Hold WebDavTools to the four quality gates
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4091, BL-4093, BL-4094, BL-4303, BL-4304, BL-4305, BL-4373]
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4374 — Extension: Hold WebDavTools to the four quality gates

## Goal

`Curl.Extension.WebDavTools.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.

## Context

Split from BL-4131. A previous run measured WebDavTools at 100% line and branch coverage with two members over complexity 10 (`WebDavSyncTransferHook.DownloadAsync` Cx 18, `WebDavCollectionListing.ListAsync` Cx 12); both were split into smaller methods (`PropfindAsync`, `Interpret`, `FetchAsync`, `Keep`) and the run did not re-measure. Composition already passed with its catalogue lines unchanged, so it needs no re-run.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Extension.WebDavTools.UnitLibrary` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member.
- [x] Any member outside a gate is fixed (code split or tests added) and the run repeated until none fails.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4131 (over its cost cap). Depends on BL-4373, which lands the code.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: a37f1904 0d68fc99 3d836d5e.
- 2026-10-30: Keep was Cx 18; extracted FailureOf. Re-measured: 100% line/branch, 67 members, 0 failing, worst CRAP 10.
- 2026-10-30: Doing -> Done. WebDavTools meets all four gates
