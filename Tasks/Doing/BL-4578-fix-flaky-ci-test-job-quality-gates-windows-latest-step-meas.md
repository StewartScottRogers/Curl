---
id: BL-4578
title: Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-02
completed:
---
# BL-4578 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 41537057745 (https://github.com/StewartScottRogers/Curl/actions/runs/41537057745). First failing commit: 040a9c35.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; Amqp1ReferenceContainer.AnswerAsync (async) is outside a gate: branch, complexity; Amqp1ReferenceContainer.Accepts(Curl.Conformance.Extended.Amqp1.Amqp1ReferenceDescribed, Curl.Conformance.Extended.Amqp1.Amqp1ReferenceSymbol[]) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 41537057745 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
