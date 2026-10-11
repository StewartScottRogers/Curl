---
id: BL-2093
title: Fix CI failure test303 on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-11
completed:
---
# BL-2093 — Fix CI failure test303 on Linux

## Goal

`test303` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test303` failed on Linux in CI run 38178740830 (https://github.com/StewartScottRogers/Curl/actions/runs/38178740830). First failing commit: 0d182629.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38178740830 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test303` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
