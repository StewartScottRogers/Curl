---
id: BL-2342
title: Verify GF-0082 test60 measures match on Linux and macOS after BL-2341
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
# BL-2342 — Verify GF-0082 test60 measures match on Linux and macOS after BL-2341

## Goal

A gap-platforms run on a branch holding BL-2341's fix measures `behaviour:test60` as `match` on Linux and macOS, or files what still differs.

## Context

- BL-2341 (ADR-0525) made a chunked `-T -` upload send its last data chunk and the closing `0\r\n\r\n` in one write, as curl fills its upload buffer before sending. GF-0082's test60 item showed the closing chunk lost (Linux, request cut at byte 179) or exit 56 (macOS) because the closing chunk went out in a second write after the server had answered and closed.
- Lanes cannot run the gap office's tools, so this is interactive only: `gh workflow run gap-platforms.yml -f ref=<branch with BL-2341>` once BL-2341 is on `work/dark-factory`.
- `lane: no` stays: the step itself needs the gap office's tools, which lanes may not run.

## Acceptance criteria

- [ ] A gap-platforms run on a ref containing BL-2341's commit is linked under Notes, with `behaviour:test60`'s Linux and macOS outcomes.
- [ ] If either is not `match`, a task with the run's request bytes and exit code is filed with the board script and named under Notes.

## Notes

## Log

- 2026-10-15: Created.
