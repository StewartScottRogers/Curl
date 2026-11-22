---
id: BL-5759
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitTests/NatsTlsUpgradeTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5759 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 45059360804 (https://github.com/StewartScottRogers/Curl/actions/runs/45059360804). First failing commit: 94f15435.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; NatsSession.ReadPlainInfoLineAsync (async) is outside a gate: branch

Lanes test only on Windows, so reproduce with `gh run view 45059360804 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The gate failure was a branch in NatsSession.ReadPlainInfoLineAsync (exception filter: cancelled by the caller before the silence timer fires) that no test reached. Added a test for it; Measure-CodeQuality.ps1 on Curl.Extended.Protocol.Nats.UnitLibrary reports 0 failing members. Platform-neutral; CI criterion met by that.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 10cc6e68.
