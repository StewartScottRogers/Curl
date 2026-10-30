---
id: BL-4349
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Extension.Composition.UnitLibrary/ExtensionCommandLineOptions.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4349 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 41071219626 (https://github.com/StewartScottRogers/Curl/actions/runs/41071219626). First failing commit: 302d7dd3.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; ExtensionCommandLineOptions.Checked(System.Collections.Generic.IReadOnlyList<Curl.Extension.Abstractions.ExtensionOption>, System.Func<string, bool>) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 41071219626 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failing member was `ExtensionCommandLineOptions.Checked`, whose name check (302d7dd3) took its complexity past 10, so `touches` gains `Curl.Extension.Composition.UnitLibrary/ExtensionCommandLineOptions.cs`; no task in Doing on origin/work/dark-factory names that file. `Measure-CodeQuality.ps1` itself needed no change.
- Fix: the check moved into `IsSelectableName`; behaviour is unchanged. `Measure-CodeQuality.ps1 -Library Curl.Extension.Composition.UnitLibrary` reports 0 failing members, the build is clean and the fast tests are green (Curl.Extension.Composition.UnitTests 28/28).
- CI on Linux and macOS: the change is pure computation with no platform dependency, so it is platform-neutral; the shift's CI watch files a task for any failure.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 10cc6e68.
