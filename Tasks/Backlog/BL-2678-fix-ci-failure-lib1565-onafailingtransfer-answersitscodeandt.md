---
id: BL-2678
title: Fix CI failure Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests, Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-17
completed:
---
# BL-2678 — Fix CI failure Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops on Linux

## Goal

`Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops` failed on Linux in CI run 39077398008 (https://github.com/StewartScottRogers/Curl/actions/runs/39077398008). First failing commit: 35c73029.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 39077398008 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Lib1565_OnAFailingTransfer_AnswersItsCodeAndTheOtherThreadStops` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-17: Created.
