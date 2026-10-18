---
id: BL-2781
title: RtspProtocolHandler sends DESCRIBE's Accept-Encoding and SETUP and DESCRIBE time conditions as lib/rtsp.c does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2781 — RtspProtocolHandler sends DESCRIBE's Accept-Encoding and SETUP and DESCRIBE time conditions as lib/rtsp.c does

## Goal

A libcurl `DESCRIBE` sends `Accept-Encoding` when `CURLOPT_ACCEPT_ENCODING` is set and no custom header names it, and a libcurl `SETUP` or `DESCRIBE` sends the time condition header `Curl_add_timecondition` writes, both in curl 8.22.0's `lib/rtsp.c` order.

## Context

Left out of BL-2751 (ADR-0691, Consequences). `RtspRequestFormatter.Format` writes the libcurl requests from `RtspRequestPlan`; in 8.22.0's `rtsp_do`, `Accept-Encoding` follows `Accept`, and the time condition goes after `Authorization` and before the custom headers, for `SETUP` and `DESCRIBE` only. Add the setting to `RtspRequestSettings` or read it from the transfer context, whichever the context already carries (`HttpRequestOptions.Compressed`, `ITransferContext` time condition). Read `lib/rtsp.c` and `lib/http.c` (`Curl_add_timecondition`) at tag `curl-8_22_0` before pinning bytes; the tool's `OPTIONS` must not change.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Rtsp.UnitTests` pins a libcurl `DESCRIBE`'s `Accept-Encoding` line and its place after `Accept`, and another that a custom `Accept-Encoding` header suppresses it.
- [x] A unit test pins the time condition header on a libcurl `SETUP` and on a `DESCRIBE`, and that `OPTIONS` sends none.
- [x] Every existing `Curl.Protocol.Rtsp.UnitTests` test passes unchanged.
- [x] `Curl.Protocol.Rtsp.UnitLibrary` keeps 100% line and branch coverage and no method past the complexity gate (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary`).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Accept-Encoding: the transfer context carries only `HttpRequestOptions.Compressed` (a bool), so a compressed libcurl `DESCRIBE` sends the HTTP library's value `deflate, gzip, br, zstd` (ADR-0287), the list libcurl writes for `CURLOPT_ACCEPT_ENCODING ""`. Written after `Accept` and before `Range`, as `rtsp_do`'s format string orders transport, accept, accept-encoding, range, referrer, user-agent, userpwd. A custom header naming it suppresses it (`Curl_checkheaders`).
- Time condition: read from `ITransferContext.TimeCondition`; written after `Authorization` and before the custom headers on `SETUP` and `DESCRIBE` only, as `rtsp_do` calls `Curl_add_timecondition` there; a custom header of the same name suppresses it, as in `http.c`. The tool's `OPTIONS *` sends none (pinned).
- The date formatter is copied into `RtspConditionDate` from the HTTP library's `HttpConditionDate` because protocol libraries never reference each other; both cases past year 9999 (January and March) are pinned for branch coverage.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` - 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 85f6256d 17037253 d09fd564 65359435 3d836d5e.
- 2026-10-18: Doing -> Done. A libcurl DESCRIBE sends Accept-Encoding for --compressed after Accept, and SETUP and DESCRIBE send the -z time condition after Authorization, as lib/rtsp.c does
