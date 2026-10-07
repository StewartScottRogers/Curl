---
id: BL-1650
title: Fix CI failure Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-07
completed:
---
# BL-1650 — Fix CI failure Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical on Linux and macOS

## Goal

`Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical` failed on Linux and macOS in CI run 37694677072 (https://github.com/StewartScottRogers/Curl/actions/runs/37694677072). First failing commit: 5c771af7.

    Assertion failed. Expected condition to be false.

Lanes test only on Windows, so reproduce with `gh run view 37694677072 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-07: Created.
