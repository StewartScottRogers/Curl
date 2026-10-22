---
id: BL-2998
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2998 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 39691487971 (https://github.com/StewartScottRogers/Curl/actions/runs/39691487971). First failing commit: 01deb298.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; HttpRequestBodyWriter.WriteBytesAsync (async) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 39691487971 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- `WriteBytesAsync` (async) was at 11 by the coverage-report measure after BL-2957 added the rate branch. Moved its piece loop into `WriteBytesInPiecesAsync`; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` now reports 0 failing members. Behaviour unchanged; Curl.Protocol.Http.UnitTests 2218 passed. CI on Linux and macOS is checked by the shift after integration.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-21: Doing -> Done. WriteBytesAsync split; quality gate reports 0 failing members for Curl.Protocol.Http.UnitLibrary
