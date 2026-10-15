---
id: BL-2251
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-14
completed:
---
# BL-2251 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 38666857143 (https://github.com/StewartScottRogers/Curl/actions/runs/38666857143). First failing commit: aa14b025.

    [36;1mforeach ($m in @((Get-Content "$env:RUNNER_TEMP\coverage.json" -Raw | ConvertFrom-Json).failing)) {[0m

Lanes test only on Windows, so reproduce with `gh run view 38666857143 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
