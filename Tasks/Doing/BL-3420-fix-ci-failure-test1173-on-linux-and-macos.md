---
id: BL-3420
title: Fix CI failure test1173: on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3420 — Fix CI failure test1173: on Linux and macOS

## Goal

`test1173:` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1173:` failed on Linux and macOS in CI run 40034290028 (https://github.com/StewartScottRogers/Curl/actions/runs/40034290028). First failing commit: 93ed8666.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 40034290028 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1173:` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 89f07da3.
