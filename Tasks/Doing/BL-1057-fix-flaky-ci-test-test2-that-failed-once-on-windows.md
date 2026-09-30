---
id: BL-1057
title: Fix flaky CI test test2 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-09-30
completed:
---
# BL-1057 — Fix flaky CI test test2 that failed once on Windows

## Goal

`test2` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `test2` failed on Windows in CI run 36786079169 (https://github.com/StewartScottRogers/Curl/actions/runs/36786079169). First failing commit: 4e66f3f1.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 36786079169 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `test2` fail intermittently is named under Notes and removed.
- [ ] `test2` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
