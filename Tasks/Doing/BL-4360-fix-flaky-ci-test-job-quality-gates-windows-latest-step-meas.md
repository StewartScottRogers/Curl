---
id: BL-4360
title: Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.StructuredFields.UnitLibrary/StructuredFieldParser.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4360 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 41109338340 (https://github.com/StewartScottRogers/Curl/actions/runs/41109338340). First failing commit: 21187411.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CertificateCheckHooks.CheckedAsync (async) is outside a gate: line, branch, complexity; StructuredFieldParser.ReadNumber() is outside a gate: complexity; StructuredFieldParser.ReadDelimitedBareItem() is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 41109338340 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
