---
id: BL-4017
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-28
completed:
---
# BL-4017 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40801399142 (https://github.com/StewartScottRogers/Curl/actions/runs/40801399142). First failing commit: fcc2b504.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; MultipartFormBodyBuilder.MissingFileFailure(Curl.Core.Multipart.MultipartBodySegments, Curl.Core.Multipart.MultipartFormPart, string, Curl.Core.Multipart.MultipartNameEscaping, bool, Curl.Core.Multipart.MultipartPartEncoder) is outside a gate: branch; ManagedEasyTransfer.PerformBuildingMimePostAsync (async) is outside a gate: branch

Lanes test only on Windows, so reproduce with `gh run view 40801399142 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 10cc6e68.
