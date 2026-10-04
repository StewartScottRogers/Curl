---
id: BL-1441
title: Fix flaky CI test test29 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-04
completed:
---
# BL-1441 — Fix flaky CI test test29 that failed once on Windows

## Goal

`test29` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `test29` failed on Windows in CI run 37187531762 (https://github.com/StewartScottRogers/Curl/actions/runs/37187531762). First failing commit: 95363aaa.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 37187531762 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `test29` fail intermittently is named under Notes and removed.
- [ ] `test29` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-04: Created.
