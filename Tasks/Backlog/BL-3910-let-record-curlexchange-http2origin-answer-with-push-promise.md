---
id: BL-3910
title: Let Record-CurlExchange -Http2Origin answer with PUSH_PROMISE, a chosen :status value or no :status
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-26
completed:
---
# BL-3910 — Let Record-CurlExchange -Http2Origin answer with PUSH_PROMISE, a chosen :status value or no :status

## Goal

`Record-CurlExchange.ps1 -Http2Origin prior-knowledge` can answer curl's HTTP/2 request with a PUSH_PROMISE (push disabled by the client), with a HEADERS block whose `:status` is a chosen string (`2x0`, `20`, `200 `), or with a HEADERS block that has no `:status`, logging the frames both ways as it does now, so real curl can be measured for CGR-142 (BL-3671).

## Context

CGR-142 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) compares curl and Curl for `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x` against server scenarios `push`, `statusbad` (`:status 2x0`), `statusspace`, `statusshort` and `nostatus`; the report's server was a Python script that is gone and Python is not allowed. `-Http2Origin` today answers only with `:status: 200` plus a body, or `-Http2ResetCode`. Real curl with nghttp2 on Windows: curl.se's `curl-8.22.0_1-win64-mingw.zip` (see BL-658 Notes), unzipped into its own empty directory and passed with `-Curl`.

Suggested shape: one new parameter, e.g. `-Http2Answer push|status:<value>|nostatus`, each scenario its own small function. For `push`, send PUSH_PROMISE on stream 1 (END_HEADERS, promised stream 2, a minimal request header block) before the response HEADERS, as nghttp2's server does.

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1 -Http2Origin prior-knowledge` accepts the new scenarios, and its help (`.PARAMETER` block with an example) describes them.
- [ ] Each scenario run against the curl.se nghttp2 curl 8.22.0 records curl's exit code, stdout, stderr and the frames both ways; the results are recorded in this task's Notes for BL-3671 to use.
- [ ] Existing `-Http2Origin` runs (`-Http2ResetCode`, plain 200) behave as before.

## Notes

- Filed by BL-3671's run (lane 9): its measurement needs these server answers.

## Log

- 2026-10-26: Created.
