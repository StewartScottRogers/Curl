---
id: BL-3421
title: Fix ApiDifferentialTests KeyNotFoundException (A, api-get) in DifferentialProcessRunner.WorkingDirectoryOf when CURL_DIFFERENTIAL_REFERENCE is set
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3405]
touches: [Curl.Conformance.Differential.IntegrationTests/Host, Curl.Conformance.Differential.IntegrationTests/Api]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3421 — Fix ApiDifferentialTests KeyNotFoundException (A, api-get) in DifferentialProcessRunner.WorkingDirectoryOf when CURL_DIFFERENTIAL_REFERENCE is set

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

Task file was a stub. Cause: ApiCaseRunner never registered the in-process "A" working directory, so WorkingDirectoryOf threw KeyNotFoundException. Fix: DifferentialProcessRunner.CreateWorkingDirectory (shared with RunAsync), called by ApiCaseRunner before each API run. Verified ApiDifferentialTests passes against msys curl; build clean.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. ApiDifferentialTests no longer throws KeyNotFoundException for the in-process A run
