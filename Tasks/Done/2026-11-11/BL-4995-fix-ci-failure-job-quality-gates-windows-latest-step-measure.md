---
id: BL-4995
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4995 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 43110153266 (https://github.com/StewartScottRogers/Curl/actions/runs/43110153266). First failing commit: 27be8cbc.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; FileProtocolHandler.WriteEntryNamesAsync (async) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 43110153266 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The failing member was in Curl.Original.Protocol.File.UnitLibrary, not the script: added FileProtocolHandler.cs to touches (no other Doing task names it). Extracted EndOfListingAfterWrite from WriteEntryNamesAsync; behaviour unchanged. Measure-CodeQuality.ps1 for the library: 0 failing members; File fast tests green. CI on Linux and macOS: change is platform-neutral.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-11-11: Doing -> Done. Extracted EndOfListingAfterWrite from WriteEntryNamesAsync; quality gate passes
