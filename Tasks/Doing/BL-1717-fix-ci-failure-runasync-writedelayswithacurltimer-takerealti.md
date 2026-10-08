---
id: BL-1717
title: Fix CI failure RunAsync_WriteDelaysWithACurlTimer_TakeRealTime on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-07
completed:
---
# BL-1717 — Fix CI failure RunAsync_WriteDelaysWithACurlTimer_TakeRealTime on Windows

## Goal

`RunAsync_WriteDelaysWithACurlTimer_TakeRealTime` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_WriteDelaysWithACurlTimer_TakeRealTime` failed on Windows in CI run 37733074018 (https://github.com/StewartScottRogers/Curl/actions/runs/37733074018). First failing commit: 711fbdf0.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37733074018 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_WriteDelaysWithACurlTimer_TakeRealTime` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-08: Backlog -> Doing.
