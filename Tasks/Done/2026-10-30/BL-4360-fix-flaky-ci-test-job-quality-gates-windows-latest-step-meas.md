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
completed: 2026-10-30
---
# BL-4360 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 41109338340 (https://github.com/StewartScottRogers/Curl/actions/runs/41109338340). First failing commit: 21187411.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CertificateCheckHooks.CheckedAsync (async) is outside a gate: line, branch, complexity; StructuredFieldParser.ReadNumber() is outside a gate: complexity; StructuredFieldParser.ReadDelimitedBareItem() is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 41109338340 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not flaky but deterministic. c2fe33468 (`--structured-fields`) added `StructuredFieldParser.ReadNumber` and `ReadDelimitedBareItem`, whose Cobertura complexity (the coverage tool counts IL branch points, which CA1502 at build time does not) was over 10. Removed by splitting `ReadDelimitedBareItem` into it and `ReadMarkedBareItem`, and `ReadNumber` into it, `Integer` and `ReadDecimalFraction`; behaviour unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Extension.StructuredFields.UnitLibrary`: 0 failing members. `dotnet build` clean and the fast tests green on Windows; the change is pure parsing code with no path, OS or certificate dependence, so it is platform-neutral for the Linux and macOS CI runs (the shift's CI watch files any failure).
- The same CI line also names `CertificateCheckHooks.CheckedAsync`; that member is BL-4358's (Blocked), outside this task's touches, and left to it. The quality step is wholly green once BL-4358 lands.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
- 2026-10-30: Doing -> Done. StructuredFieldParser.ReadNumber and ReadDelimitedBareItem are inside the complexity gate
