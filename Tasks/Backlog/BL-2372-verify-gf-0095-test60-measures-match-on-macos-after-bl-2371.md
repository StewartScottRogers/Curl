---
id: BL-2372
title: Verify GF-0095 test60 measures match on macOS after BL-2371
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Tasks]
lane: no
requirement: none
created: 2026-10-15
completed:
---
# BL-2372 — Verify GF-0095 test60 measures match on macOS after BL-2371

## Goal

A gap-platforms run on a ref holding BL-2371's fix measures `behaviour:test60` as `match` on macOS, or files what still differs.

## Context

- BL-2371 (ADR-0545, extending ADR-0525) made a chunked `-T -` upload sent after an expired `100 Continue` wait send its last data chunk and `0\r\n\r\n` in one `SendUnlessStoppedAsync` write. GF-0095 recorded exit 56 on macOS where curl 8.22.0 exits 0, because the closing chunk went out in a second write after the test server had answered and closed.
- The fix is pinned on every platform by `HttpRequestBodyWriterTests.WriteAsync_ChunkedStdinAfterTheContinueWaitRanOut_SendsDataAndLastChunkInOneWrite`, but only the gap office's own measurement closes the finding (ADR-0433 decision 5).
- Lanes cannot run the gap office's tools, so this is interactive only: `gh workflow run gap-platforms.yml -f ref=<branch with BL-2371>` once BL-2371 is on `work/dark-factory`. BL-2342 (the same check for BL-2341 on Linux and macOS) can share the run.
- `lane: no` stays: the step itself needs the gap office's tools, which lanes may not run.

## Acceptance criteria

- [ ] A gap-platforms run on a ref containing BL-2371's commit is linked under Notes, with `behaviour:test60`'s macOS outcome.
- [ ] If it is not `match`, a task with the run's request bytes and exit code is filed with the board script and named under Notes.

## Notes

## Log

- 2026-10-15: Created.
