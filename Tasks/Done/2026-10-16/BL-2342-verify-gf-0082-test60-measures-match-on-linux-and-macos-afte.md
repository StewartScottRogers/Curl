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
completed: 2026-10-16
---
# BL-2342 — Verify GF-0082 test60 measures match on Linux and macOS after BL-2341

## Goal

A gap-platforms run on a branch holding BL-2341's fix measures `behaviour:test60` as `match` on Linux and macOS, or files what still differs.

## Context

- BL-2341 (ADR-0525) made a chunked `-T -` upload send its last data chunk and the closing `0\r\n\r\n` in one write, as curl fills its upload buffer before sending. GF-0082's test60 item showed the closing chunk lost (Linux, request cut at byte 179) or exit 56 (macOS) because the closing chunk went out in a second write after the server had answered and closed.
- Lanes cannot run the gap office's tools, so this is interactive only: `gh workflow run gap-platforms.yml -f ref=<branch with BL-2341>` once BL-2341 is on `work/dark-factory`.
- `lane: no` stays: the step itself needs the gap office's tools, which lanes may not run.

## Acceptance criteria

- [x] A gap-platforms run on a ref containing BL-2341's commit is linked under Notes, with `behaviour:test60`'s Linux and macOS outcomes.
- [x] If either is not `match`, a task with the run's request bytes and exit code is filed with the board script and named under Notes.

## Notes

- Run: https://github.com/StewartScottRogers/Curl/actions/runs/38912832952, `work/dark-factory` 4e63a4fd9ca02affcad51bd4bee1eff1c5bc8fa6 (holds BL-2341's 9d7cb9b92 and BL-2371's d6c0e44de). An earlier dispatch with a 10-character commit (run 38912291199) failed at checkout: `actions/checkout` needs the full SHA.
- Linux: `behaviour:test60` is `gap`. macOS: `gap`. Both: evidence `was match; in-process and out-of-process differ`, expected `reference curl exits 0; stdout 0 bytes`, actual `request differs at byte 179`. The exit codes agree; byte 179 is where `0\r\n\r\n` starts, so the built binary still sends no closing chunk. The in-process harness passes the case.
- Filed BL-2483 to fix the out-of-process path, with the request-byte breakdown and recorder settings.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Re-measured on run 38912832952: test60 is still a gap on Linux and macOS out of process (request differs at byte 179, closing chunk missing); BL-2483 filed
