---
id: BL-2647
title: Record-CurlExchange.ps1: end a chunked request at its closing chunk even with a Content-Length, as sws does (test60)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2647 — Record-CurlExchange.ps1: end a chunked request at its closing chunk even with a Content-Length, as sws does (test60)

## Goal

`Record-CurlExchange.ps1`'s HTTP server takes a request whose head has `Transfer-Encoding: chunked` as complete only once its closing chunk (`0\r\n\r\n`) has arrived, whatever `Content-Length` the head also carries, as upstream's `tests/server/sws.c` (curl 8.22.0) does.

## Context

- `Test-RequestComplete` in `$serveConnections` checks `Content-Length` before `Transfer-Encoding: chunked`. Upstream test60 (`-T - -H "Content-Length: 1"`) sends both, so the recorder answers as soon as one body byte is in. curl 8.22.0 sends the data chunk and the closing chunk in two writes (25 bytes, then 5). The response holds `swsclose`, so the recorder closes at once, and the reference's `request.bin` stops at byte 179 without the closing chunk. Curl sends both in one write and keeps it. The gap office's cross-check then records `behaviour:test60` as a gap on Linux and macOS (gap-platforms run 39063808349, BL-2522, ADR-0648).
- `sws.c`'s `ProcessRequest` sets `chunked` on the `Transfer-Encoding: chunked` line and then returns done only at `\r\n0\r\n\r\n`, so chunked wins over `Content-Length` (RFC 9112 section 6.3 rule 3 agrees).
- `-RespondAfterBodyBytes` keeps its meaning: it answers after that many body bytes whatever the framing.

## Acceptance criteria

- [x] `Test-RequestComplete` checks `Transfer-Encoding: chunked` before `Content-Length`, and the script's help for the HTTP server says so.
- [x] Measured with the built Curl.Console and real curl on Windows: `Record-CurlExchange.ps1 -Response 'HTTP/1.0 200 OK swsclose\r\n\r\nblablabla\n' -StandardInput 'more than one byte\n' -RequestReadMilliseconds 3000 -CurlArgs http://127.0.0.1:<port>/bzz/60,-T,-,-H,'Content-Length: 1'` records a `request.bin` ending in `0\r\n\r\n` for both, with the commands and byte counts under Notes.
- [x] A request with `Content-Length` and no chunked coding still completes at its length (one measurement under Notes).

## Notes
## Notes

Cause of the $9.53 (AF-0245), from the finding's own breakdown (the run logs are not in this checkout, so nothing was re-measured):
- $3.38 on two runs that ended in requeues: BL-2394 was split (19 turns, $1.47), then found to wait on BL-2479 (23 turns, $1.91). This is the requeue-early rule already in the lane prompt (AF-0147): find every prerequisite in the first pass. Each requeue was a legitimate outcome, not a bug.
- $5.66 on a 90-turn final run, plus $0.48 on a rebase-conflict resolve run. The task was large; the resolve run is the shift's normal conflict path.
- The denied `git stash list` at the format step was already fixed: `Restore-TaskStash` in RunDarkFactory.ps1 applies the stash itself so a lane never needs `git stash list` (AF-0091, AF-0099).

Decision: no avoidable waste remains that a code change would remove; the cost is recorded as unavoidable for the past run, and the history cannot change (task-done-historical closes the finding). No code changed.
Measured on Windows (built Curl.Console, real curl): `Record-CurlExchange.ps1 -Port <p> -Response 'HTTP/1.0 200 OK swsclose\r\n\r\nblablabla\n' -StandardInput 'more than one byte\n' -RequestReadMilliseconds 3000 -CurlArgs http://127.0.0.1:<p>/bzz/60,-T,-,-H,'Content-Length: 1'` -> request.bin 184 bytes for both, ending `30 0d 0a 0d 0a` (`0\r\n\r\n`). Content-Length only: `-d abcde` -> 154 bytes, completes at its length, ends `abcde`.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 1ff8bb94 91c8b6de bf422777.
- 2026-10-17: Doing -> Done. Chunked now wins over Content-Length in Test-RequestComplete; measured test60 request ends 0\r\n\r\n
