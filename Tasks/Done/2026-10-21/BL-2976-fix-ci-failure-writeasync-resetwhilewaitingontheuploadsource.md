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
completed: 2026-10-21
---
# BL-2976 — Fix CI failure WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords on Linux

## Goal

`WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords` failed on Linux in CI run 39664616758 (https://github.com/StewartScottRogers/Curl/actions/runs/39664616758). First failing commit: 5fd6ff02.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 39664616758 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test's `LateReadStream` finished its read after `Task.Yield`. On Linux's thread pool the read could complete before `HttpRequestBodyWriter` checked `IsCompleted`, so `waitedOnSource` stayed false and the failed write stayed exit 55. The race was in the test, not the product. The fixture now returns an `IValueTaskSource` that stays pending until the caller registers its continuation, so the writer always waits on the read, on every platform. No production change, so `Curl.Protocol.Http.UnitLibrary` is untouched.
- Verified locally: `Curl.Protocol.Http.UnitTests` 2215 passed, 0 failed, 20 skipped; `dotnet build` 0 errors. CI on Linux and macOS runs on the shift's push. If it is still red, the coordinator's CI watch files the failure again.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 12475fd2.
- 2026-10-21: Doing -> Done. Waiting-piece reset test no longer races the thread pool; its upload read stays pending until awaited on every platform
