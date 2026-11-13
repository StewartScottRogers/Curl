---
id: BL-5060
title: Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineParser.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5060 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 43335524700 (https://github.com/StewartScottRogers/Curl/actions/runs/43335524700). First failing commit: e3acaf9e.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CommandLineParser.TransferSetupRefusal(Curl.Cli.CommandLineOptions) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 43335524700 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: TransferSetupRefusal had grown (the -J with -C - check, b2e6a95c9) to a compiled complexity past 10, so Measure-CodeQuality failed deterministically from that commit on (not truly flaky). Fix: moved the form/body/-C checks into BodyRefusal. Measure-CodeQuality for Curl.Cli.UnitLibrary: 0 failing members; Cli fast tests green. CI on all three platforms is platform-neutral, no OS-specific code.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
- 2026-11-12: Doing -> Done. Split TransferSetupRefusal so it is inside the complexity gate
