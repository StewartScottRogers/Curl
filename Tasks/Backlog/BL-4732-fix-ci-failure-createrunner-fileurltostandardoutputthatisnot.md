---
id: BL-4732
title: Fix CI failure CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-03
completed:
---
# BL-4732 — Fix CI failure CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter on Linux

## Goal

`CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter` failed on Linux in CI run 41655977963 (https://github.com/StewartScottRogers/Curl/actions/runs/41655977963). First failing commit: 85f7b9d8.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41655977963 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
