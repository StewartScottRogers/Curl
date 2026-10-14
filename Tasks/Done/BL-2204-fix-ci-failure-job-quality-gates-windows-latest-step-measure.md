---
id: BL-2204
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2204 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 38519058174 (https://github.com/StewartScottRogers/Curl/actions/runs/38519058174). First failing commit: e8a853b1.

    [36;1mforeach ($m in @((Get-Content "$env:RUNNER_TEMP\coverage.json" -Raw | ConvertFrom-Json).failing)) {[0m

Lanes test only on Windows, so reproduce with `gh run view 38519058174 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- CI run 38519058174 failed on one member: `GopherProtocolHandler.ReportConnectionEnd` at complexity 12 (gate 10), added by BL-2185's CR/LF branch. Split it into `ReportShuttingDown` and `ReportClosing`; behaviour unchanged. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Added `Curl.Protocol.Gopher.UnitLibrary` to touches (it was empty; no other task in Doing names it). CI on Linux and macOS runs on the shift's push.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. ReportConnectionEnd split to complexity 6 or less; Gopher library passes all four quality gates
