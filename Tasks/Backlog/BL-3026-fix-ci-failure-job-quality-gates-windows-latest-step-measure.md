---
id: BL-3026
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-21
completed:
---
# BL-3026 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 39718635967 (https://github.com/StewartScottRogers/Curl/actions/runs/39718635967). First failing commit: d6d35bf6.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; Unit2413ConnectionPeer.Mismatch(string, Curl.Conformance.UnitProgram.Dns.Easy.Unit2413ConnectionPeer.Peer, Curl.Conformance.UnitProgram.Dns.Easy.Unit2413ConnectionPeer.Peer) is outside a gate: complexity; ConnectionPeer.Create(string, string, int) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 39718635967 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-21: Created.
