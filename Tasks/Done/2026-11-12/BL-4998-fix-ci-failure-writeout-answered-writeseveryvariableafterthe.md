---
id: BL-4998
title: Fix CI failure WriteOut_Answered_WritesEveryVariableAfterTheBody on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4998 — Fix CI failure WriteOut_Answered_WritesEveryVariableAfterTheBody on Linux and Windows

## Goal

`WriteOut_Answered_WritesEveryVariableAfterTheBody` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `WriteOut_Answered_WritesEveryVariableAfterTheBody` failed on Linux and Windows in CI run 43127888162 (https://github.com/StewartScottRogers/Curl/actions/runs/43127888162). First failing commit: 99659d3d.

    Assertion failed. Expected strings to be equal.

Failing data rows: `WriteOut_Answered_WritesEveryVariableAfterTheBody ("grpc")`, `WriteOut_Answered_WritesEveryVariableAfterTheBody ("grpcs")`.

Lanes test only on Windows, so reproduce with `gh run view 43127888162 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `WriteOut_Answered_WritesEveryVariableAfterTheBody` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: BL-4938 made the grpc handler return a TransferReport, which replaced the engine default whose DownloadSize was the bytes written, so %{size_download} printed 0. The handler now sets DownloadSize to the bytes written; the report is non-null by construction (GrpcCallResult.Report). Platform-neutral; grpc and conformance fast tests green, grpc library quality 100/100.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 047f8d90.
- 2026-11-11: Doing -> Done. grpc report keeps size_download
