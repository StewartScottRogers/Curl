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
completed:
---
# BL-2647 — Record-CurlExchange.ps1: end a chunked request at its closing chunk even with a Content-Length, as sws does (test60)

## Goal

`Record-CurlExchange.ps1`'s HTTP server takes a request whose head has `Transfer-Encoding: chunked` as complete only once its closing chunk (`0\r\n\r\n`) has arrived, whatever `Content-Length` the head also carries, as upstream's `tests/server/sws.c` (curl 8.22.0) does.

## Context

- `Test-RequestComplete` in `$serveConnections` checks `Content-Length` before `Transfer-Encoding: chunked`. Upstream test60 (`-T - -H "Content-Length: 1"`) sends both, so the recorder answers as soon as one body byte is in. curl 8.22.0 sends the data chunk and the closing chunk in two writes (25 bytes, then 5). The response holds `swsclose`, so the recorder closes at once, and the reference's `request.bin` stops at byte 179 without the closing chunk. Curl sends both in one write and keeps it. The gap office's cross-check then records `behaviour:test60` as a gap on Linux and macOS (gap-platforms run 39063808349, BL-2522, ADR-0648).
- `sws.c`'s `ProcessRequest` sets `chunked` on the `Transfer-Encoding: chunked` line and then returns done only at `\r\n0\r\n\r\n`, so chunked wins over `Content-Length` (RFC 9112 section 6.3 rule 3 agrees).
- `-RespondAfterBodyBytes` keeps its meaning: it answers after that many body bytes whatever the framing.

## Acceptance criteria

- [ ] `Test-RequestComplete` checks `Transfer-Encoding: chunked` before `Content-Length`, and the script's help for the HTTP server says so.
- [ ] Measured with the built Curl.Console and real curl on Windows: `Record-CurlExchange.ps1 -Response 'HTTP/1.0 200 OK swsclose\r\n\r\nblablabla\n' -StandardInput 'more than one byte\n' -RequestReadMilliseconds 3000 -CurlArgs http://127.0.0.1:<port>/bzz/60,-T,-,-H,'Content-Length: 1'` records a `request.bin` ending in `0\r\n\r\n` for both, with the commands and byte counts under Notes.
- [ ] A request with `Content-Length` and no chunked coding still completes at its length (one measurement under Notes).

## Notes

## Log

- 2026-10-17: Created.
