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
completed:
---
# BL-2781 — RtspProtocolHandler sends DESCRIBE's Accept-Encoding and SETUP and DESCRIBE time conditions as lib/rtsp.c does

## Goal

A libcurl `DESCRIBE` sends `Accept-Encoding` when `CURLOPT_ACCEPT_ENCODING` is set and no custom header names it, and a libcurl `SETUP` or `DESCRIBE` sends the time condition header `Curl_add_timecondition` writes, both in curl 8.22.0's `lib/rtsp.c` order.

## Context

Left out of BL-2751 (ADR-0691, Consequences). `RtspRequestFormatter.Format` writes the libcurl requests from `RtspRequestPlan`; in 8.22.0's `rtsp_do`, `Accept-Encoding` follows `Accept`, and the time condition goes after `Authorization` and before the custom headers, for `SETUP` and `DESCRIBE` only. Add the setting to `RtspRequestSettings` or read it from the transfer context, whichever the context already carries (`HttpRequestOptions.Compressed`, `ITransferContext` time condition). Read `lib/rtsp.c` and `lib/http.c` (`Curl_add_timecondition`) at tag `curl-8_22_0` before pinning bytes; the tool's `OPTIONS` must not change.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Rtsp.UnitTests` pins a libcurl `DESCRIBE`'s `Accept-Encoding` line and its place after `Accept`, and another that a custom `Accept-Encoding` header suppresses it.
- [ ] A unit test pins the time condition header on a libcurl `SETUP` and on a `DESCRIBE`, and that `OPTIONS` sends none.
- [ ] Every existing `Curl.Protocol.Rtsp.UnitTests` test passes unchanged.
- [ ] `Curl.Protocol.Rtsp.UnitLibrary` keeps 100% line and branch coverage and no method past the complexity gate (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary`).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
