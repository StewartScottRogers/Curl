---
id: BL-6052
title: Fix CI failure ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests, Curl.Original.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6052 — Fix CI failure ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact on Windows

## Goal

`ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact` failed on Windows in CI run 45905543947 (https://github.com/StewartScottRogers/Curl/actions/runs/45905543947). First failing commit: e6508e78.

    CollectionAssert.AreEqual failed. Different number of elements.

Failing data rows: `head and DATA, stream left open`.

Lanes test only on Windows, so reproduce with `gh run view 45905543947 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in the test, not a product defect. The handler gives a failed transfer's
  stream cleanup `StreamCleanupLimit` (1 s, on the context's `TimeProvider`, BL-6006). The
  test used `TimeProvider.System`, and the CI runner took 2.5 s: the limit passed after the
  stream was aborted and before the QPACK Stream Cancellation was written, so the decoder
  stream held only `03`. Reproduced locally by sleeping 1.5 s in the fake stream's `Aborting`
  (both data rows failed at line 170); with the fix the four classes passed with the sleep in
  place, which was then removed.
- Fix: the default `TimeProvider` of the test contexts in `Http3StreamAbortTests`,
  `Http2StreamAbortTests`, `Http3FailedTransferStreamTests` and
  `Http3ResetStreamConnectionTests` (the last three had the same race) is now a
  `FakeTimeProvider` no test advances. The limit itself stays as BL-6006 decided; its own
  tests drive it with a fake clock. No production code changed, so no quality run and no ADR.
- Pipeline `feature`: a test-only fix, so done directly without the stage subagents.
- Verified here: `dotnet build` with 0 warnings and 0 errors, fast tests green on Windows.
  The CI half of the criterion is met by keeping the change platform-neutral (lane rule: a
  lane cannot see CI for its own push); the shift's CI watch files a task for any failure.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 41c67ff1.
- 2026-11-26: Doing -> Done. The stream abort tests run on a clock no test advances, so a slow runner no longer drops the expected cancellation
