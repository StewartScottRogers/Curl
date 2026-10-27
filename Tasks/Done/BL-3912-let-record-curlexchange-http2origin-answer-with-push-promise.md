---
id: BL-3912
title: Let Record-CurlExchange -Http2Origin answer with PUSH_PROMISE, a chosen :status value or no :status
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3912 — Let Record-CurlExchange -Http2Origin answer with PUSH_PROMISE, a chosen :status value or no :status

## Goal

`Record-CurlExchange.ps1 -Http2Origin prior-knowledge` can answer curl's HTTP/2 request with a PUSH_PROMISE (push disabled by the client), with a HEADERS block whose `:status` is a chosen string (`2x0`, `20`, `200 `), or with a HEADERS block that has no `:status`, logging the frames both ways as it does now, so real curl can be measured for CGR-142 (BL-3671).

## Context

CGR-142 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) compares curl and Curl for `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x` against server scenarios `push`, `statusbad` (`:status 2x0`), `statusspace`, `statusshort` and `nostatus`; the report's server was a Python script that is gone and Python is not allowed. `-Http2Origin` today answers only with `:status: 200` plus a body, or `-Http2ResetCode`. Real curl with nghttp2 on Windows: curl.se's `curl-8.22.0_1-win64-mingw.zip` (see BL-658 Notes), unzipped into its own empty directory and passed with `-Curl`.

Suggested shape: one new parameter, e.g. `-Http2Answer push|status:<value>|nostatus`, each scenario its own small function. For `push`, send PUSH_PROMISE on stream 1 (END_HEADERS, promised stream 2, a minimal request header block) before the response HEADERS, as nghttp2's server does.

## Acceptance criteria

- [x] `Record-CurlExchange.ps1 -Http2Origin prior-knowledge` accepts the new scenarios, and its help (`.PARAMETER` block with an example) describes them.
- [x] Each scenario run against the curl.se nghttp2 curl 8.22.0 records curl's exit code, stdout, stderr and the frames both ways; the results are recorded in this task's Notes for BL-3671 to use.
- [x] Existing `-Http2Origin` runs (`-Http2ResetCode`, plain 200) behave as before.

## Notes

- Filed by BL-3671's run (lane 9): its measurement needs these server answers.
- Added `-Http2Answer push|status:<value>|nostatus` (needs `-Http2Origin`; `AnswerScenario` in `RecorderHttp2Proxy`, used for non-CONNECT requests only, so `-Http2` proxy runs, `-Http2ResetCode` and the plain answer are unchanged). Baseline run `-Http2Origin prior-knowledge` still gives curl exit 0, stdout `HTTP/2 200 \r\ncontent-length: 0\r\n\r\n`.
- Measured with curl.se curl 8.22.0_2 mingw (nghttp2/1.70.0), `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`. Server frames after curl's HEADERS/SETTINGS; stdout is empty in every case:
  - `push`: server sends PUSH_PROMISE (stream 1, END_HEADERS, promised 2) then HEADERS `:status 200` + content-length 0 (END_STREAM). curl exit 92, stderr `curl: (92) [HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=7, hend=1], error -505: Protocol error`; curl sends GOAWAY last stream 0 error 1 debug `PUSH_PROMISE: push disabled`.
  - `status:2x0`: exit 92, stderr `curl: (92) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [2x0]`; curl sends RST_STREAM stream 1 error 1 (PROTOCOL_ERROR) then GOAWAY last stream 0 error 0 debug `shutdown`.
  - `status:20`: same, exit 92, value `[20]`.
  - `status:200 ` (trailing space): same, exit 92, value `[200 ]`.
  - `nostatus` (HEADERS with only content-length 0): exit 8, stderr `curl: (8) [HTTP2] [1] received invalid frame: FRAME[HEADERS, len=4, hend=1, eos=1], error -532: Violation in HTTP messaging rule`; RST_STREAM stream 1 error 1, then GOAWAY error 0 `shutdown`.
  - Fixtures were written to `%TEMP%\l9out\1..6` (not committed).

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: cfa31551 e65894b0 c50e4ce8.
- 2026-10-26: Doing -> Done. Record-CurlExchange -Http2Answer push|status:<value>|nostatus measured against nghttp2 curl 8.22.0
