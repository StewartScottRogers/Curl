---
id: BL-2258
title: Fix CI failure test203 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2258 — Fix CI failure test203 on Linux and macOS

## Goal

`test203` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test203` failed on Linux and macOS in CI run 38682342325 (https://github.com/StewartScottRogers/Curl/actions/runs/38682342325). First failing commit: 8d337539.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38682342325 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test203` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
