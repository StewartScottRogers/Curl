---
id: BL-5570
title: Fix CI failure job Quality gates (windows-latest) step UNKNOWN STEP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5570 — Fix CI failure job Quality gates (windows-latest) step UNKNOWN STEP on Windows

## Goal

`job Quality gates (windows-latest) step UNKNOWN STEP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step UNKNOWN STEP` failed on Windows in CI run 44586512674 (https://github.com/StewartScottRogers/Curl/actions/runs/44586512674). First failing commit: 26f8bed2.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CommandLineOptions.get_ActsAsQuicBuild() is outside a gate: line

Lanes test only on Windows, so reproduce with `gh run view 44586512674 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step UNKNOWN STEP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The getter of `CommandLineOptions.ActsAsQuicBuild` had no reader in the fast tests (line coverage gate). Added `Curl.Cli.UnitTests/ActsAsQuicBuildTests.cs` (test file only; no production change).
Linux and macOS: test is platform-neutral.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 2e720b73.
