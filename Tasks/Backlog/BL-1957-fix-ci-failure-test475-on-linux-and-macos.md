---
id: BL-1957
title: Fix CI failure test475 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-10
completed:
---
# BL-1957 — Fix CI failure test475 on Linux and macOS

## Goal

`test475` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test475` failed on Linux and macOS in CI run 38047662088 (https://github.com/StewartScottRogers/Curl/actions/runs/38047662088). First failing commit: 7b6794f4.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38047662088 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test475` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-10: Created.
