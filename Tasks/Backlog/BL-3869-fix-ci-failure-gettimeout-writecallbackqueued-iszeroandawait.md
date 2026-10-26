---
id: BL-3869
title: Fix CI failure GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3869 — Fix CI failure GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady on Linux

## Goal

`GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady` failed on Linux in CI run 40387515096 (https://github.com/StewartScottRogers/Curl/actions/runs/40387515096). First failing commit: b6305926.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40387515096 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `GetTimeout_WriteCallbackQueued_IsZeroAndAWaitCountsItReady` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
