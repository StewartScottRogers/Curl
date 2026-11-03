---
id: BL-4666
title: Fix CI failure Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4666 — Fix CI failure Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput on Linux and macOS

## Goal

`Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput` failed on Linux and macOS in CI run 41597701882 (https://github.com/StewartScottRogers/Curl/actions/runs/41597701882). First failing commit: 4e244606.

    Assertion failed. Expected value to be greater than or equal to the lower bound.

Failing data rows: `Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput ("mqtt5s","CurlSurface:Verbose-Mqtt5s-1")`.

Lanes test only on Windows, so reproduce with `gh run view 41597701882 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 7e7c622d.
