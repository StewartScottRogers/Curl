---
id: BL-2874
title: Fix CI failure test1587 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2874 — Fix CI failure test1587 on Linux and macOS

## Goal

`test1587` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1587` failed on Linux and macOS in CI run 39362208615 (https://github.com/StewartScottRogers/Curl/actions/runs/39362208615). First failing commit: c8ae701d.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 39362208615 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1587` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-19: Created.
