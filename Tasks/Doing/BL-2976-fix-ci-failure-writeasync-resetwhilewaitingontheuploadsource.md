---
id: BL-2976
title: Fix CI failure WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitTests, Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-21
completed:
---
# BL-2976 — Fix CI failure WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords on Linux

## Goal

`WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords` failed on Linux in CI run 39664616758 (https://github.com/StewartScottRogers/Curl/actions/runs/39664616758). First failing commit: 5fd6ff02.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 39664616758 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 12475fd2.
