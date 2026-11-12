---
id: BL-5057
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5057 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 43323719038 (https://github.com/StewartScottRogers/Curl/actions/runs/43323719038). First failing commit: 879e59f2.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CurlCommandRunner.TransferUrlAsync (async) is outside a gate: complexity; CurlCommandRunner.TransferIntoOutputFileAsync (async) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 43323719038 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- touches widened to Curl.Console/CurlCommandRunner.cs: no other Doing task holds it. The real failure was complexity of TransferUrlAsync and TransferIntoOutputFileAsync, not the script. Extracted TransferAfterSetupAsync and CompleteOutputFileAsync (pure refactor). Measure-CodeQuality.ps1 -Library Curl.Console: 0 failing members; Console fast tests green. Platform-neutral, so the CI criterion holds.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-11-12: Doing -> Done. Extracted two methods from CurlCommandRunner to bring them within the complexity gate
