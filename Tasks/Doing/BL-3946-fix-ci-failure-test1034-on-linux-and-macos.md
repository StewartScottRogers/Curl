---
id: BL-3946
title: Fix CI failure test1034: on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-3946 — Fix CI failure test1034: on Linux and macOS

## Goal

`test1034:` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1034:` failed on Linux and macOS in CI run 40724749646 (https://github.com/StewartScottRogers/Curl/actions/runs/40724749646). First failing commit: 158f6fe6.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 40724749646 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1034:` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1a4e3b30.
