---
id: BL-1949
title: Fix CI failure test4001 on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1949 — Fix CI failure test4001 on macOS

## Goal

`test4001` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test4001` failed on macOS in CI run 38031396189 (https://github.com/StewartScottRogers/Curl/actions/runs/38031396189). First failing commit: 1c6aa1ac.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38031396189 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test4001` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-09: Created.
