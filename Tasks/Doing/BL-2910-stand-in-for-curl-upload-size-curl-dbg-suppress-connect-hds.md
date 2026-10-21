---
id: BL-2910
title: Stand in for CURL_UPLOAD_SIZE, CURL_DBG_SUPPRESS_CONNECT_HDS and the tool's CURL_ENTROPY WebSocket key so upstream cases 447, 2349 and 2300 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.ToolTransferStandIns.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2910 — Stand in for CURL_UPLOAD_SIZE, CURL_DBG_SUPPRESS_CONNECT_HDS and the tool's CURL_ENTROPY WebSocket key so upstream cases 447, 2349 and 2300 run

## Goal

Upstream cases 447 (`CURL_UPLOAD_SIZE=498`), 2349 (`CURL_DBG_SUPPRESS_CONNECT_HDS=1`) and 2300 (`CURL_ENTROPY=12345678` with `ws` through the curl tool) pass in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.

## Context

Split from BL-2835. Read upstream's source at tag `curl-8_22_0` for each variable: `CURL_UPLOAD_SIZE` (the upload size the tool reports for a growing `-T` file, `src/tool_operate.c`), `CURL_DBG_SUPPRESS_CONNECT_HDS` (CONNECT response headers kept out of the `-D` file, `lib/http_proxy.c`/`lib/cf-h1-proxy.c`) and the tool's WebSocket key under `CURL_ENTROPY`. ADR-0678 gave only the libtest engine `ForcedEntropyWebSocketRandomSource` (`UpstreamConformanceTests.CreateEngine`); the curl tool's dialing `CurlComposition.CreateRunner` takes no WebSocket random source, so 2300 differs at `Sec-WebSocket-Key`. Each stand-in is the harness's, behind a `CreateRunner` parameter off by default; the shipped curl never reads these variables. Report `Debug` to a case setting one with a rule in `DebugStandInRules`, its predicate in its own partial file. If the three prove more than one run, finish them in the order listed and file the rest.

## Acceptance criteria

- [ ] `PlatformFor` reports `Debug` to a case setting `CURL_UPLOAD_SIZE`, `CURL_DBG_SUPPRESS_CONNECT_HDS`, or `CURL_ENTROPY` with `ws` through the curl tool, and a unit test pins it.
- [ ] Each stand-in has a unit test that fails without it.
- [ ] Cases 447, 2349 and 2300 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: cee4fab0 ca97a32f 3b310db3 9495a595.
