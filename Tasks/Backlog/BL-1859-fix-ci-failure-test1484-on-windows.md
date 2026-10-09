---
id: BL-1859
title: Fix CI failure test1484 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1859 — Fix CI failure test1484 on Windows

## Goal

`test1484` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1484` failed on Windows in CI run 37907205689 (https://github.com/StewartScottRogers/Curl/actions/runs/37907205689). First failing commit: 85f6337b.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 37907205689 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1484` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-09: Created.
