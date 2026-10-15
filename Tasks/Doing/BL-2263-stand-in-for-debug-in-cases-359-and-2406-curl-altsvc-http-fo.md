---
id: BL-2263
title: Stand in for Debug in cases 359 and 2406 (CURL_ALTSVC_HTTP for Alt-Svc over plain http) so they run on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2245]
touches: [Curl.Conformance.UnitTests, Curl.Console]
requirement: none
created: 2026-10-14
completed:
---
# BL-2263 — Stand in for Debug in cases 359 and 2406 (CURL_ALTSVC_HTTP for Alt-Svc over plain http) so they run on Linux and macOS

## Goal

Cases 359 and 2406 run on Linux and macOS instead of skipping for the Debug feature.

## Context

Found by BL-2245 (2026-10-14): with %HTTP2TLSPORT and the TLS front in place, test359 and test2406 skip only for Debug. test2406 (--http2 -f over %HTTP2TLSPORT) uses no debug-only behaviour, so reporting Debug to it is enough. test359 needs CURL_ALTSVC_HTTP, the debug build’s switch that lets Alt-Svc be learnt over plain http (as CURL_HSTS_HTTP does for HSTS, ADR-0494, BL-2250). Start at UpstreamConformanceTests.PlatformFor in Curl.Conformance.UnitTests and the Alt-Svc cache code reached from Curl.Console. On Windows both stay SkippedOnThisPlatform for http/2 (ADR-0495).

## Acceptance criteria

- [ ] PlatformFor reports Debug to 2406 and 359, with a comment saying why, as it does for the httpsig and HSTS cases.
- [ ] Curl honours CURL_ALTSVC_HTTP in the conformance harness only (as CURL_HSTS_HTTP), with a focused unit test in the fixed library’s .UnitTests (ADR-0482).
- [ ] Each of 359 and 2406 that passes on Linux and macOS CI is in PassingUpstreamCases.txt; any that fails is named in Notes with a follow-up task.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
