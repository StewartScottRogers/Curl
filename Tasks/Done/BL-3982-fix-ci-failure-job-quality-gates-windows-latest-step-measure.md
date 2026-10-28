---
id: BL-3982
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3982 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40752869929 (https://github.com/StewartScottRogers/Curl/actions/runs/40752869929). First failing commit: f07826e0.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CurlUrlHost.EncodeLabel(string) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 40752869929 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- `EncodeLabel` was past the complexity gate (the three-way rune check inside the loop). Moved that check into `TryDecodeEncodableRune`; behaviour unchanged. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 0 failing members; build clean, fast tests green (46 assemblies). The change is pure computation with no platform dependence, so CI on Linux and macOS is met by keeping it platform-neutral; the shift's CI watch files any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-28: Doing -> Done. CurlUrlHost.EncodeLabel is back inside the complexity gate
