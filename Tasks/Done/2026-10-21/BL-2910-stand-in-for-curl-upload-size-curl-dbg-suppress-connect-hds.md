---
id: BL-2910
title: Stand in for CURL_UPLOAD_SIZE, CURL_DBG_SUPPRESS_CONNECT_HDS and the tool's CURL_ENTROPY WebSocket key so upstream cases 447, 2349 and 2300 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.ToolTransferStandIns.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2910 — Stand in for CURL_UPLOAD_SIZE, CURL_DBG_SUPPRESS_CONNECT_HDS and the tool's CURL_ENTROPY WebSocket key so upstream cases 447, 2349 and 2300 run

## Goal

Upstream cases 447 (`CURL_UPLOAD_SIZE=498`), 2349 (`CURL_DBG_SUPPRESS_CONNECT_HDS=1`) and 2300 (`CURL_ENTROPY=12345678` with `ws` through the curl tool) pass in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.

## Context

Split from BL-2835. Read upstream's source at tag `curl-8_22_0` for each variable: `CURL_UPLOAD_SIZE` (the upload size the tool reports for a growing `-T` file, `src/tool_operate.c`), `CURL_DBG_SUPPRESS_CONNECT_HDS` (CONNECT response headers kept out of the `-D` file, `lib/http_proxy.c`/`lib/cf-h1-proxy.c`) and the tool's WebSocket key under `CURL_ENTROPY`. ADR-0678 gave only the libtest engine `ForcedEntropyWebSocketRandomSource` (`UpstreamConformanceTests.CreateEngine`); the curl tool's dialing `CurlComposition.CreateRunner` takes no WebSocket random source, so 2300 differs at `Sec-WebSocket-Key`. Each stand-in is the harness's, behind a `CreateRunner` parameter off by default; the shipped curl never reads these variables. Report `Debug` to a case setting one with a rule in `DebugStandInRules`, its predicate in its own partial file. If the three prove more than one run, finish them in the order listed and file the rest.

## Acceptance criteria

- [x] `PlatformFor` reports `Debug` to a case setting `CURL_UPLOAD_SIZE`, `CURL_DBG_SUPPRESS_CONNECT_HDS`, or `CURL_ENTROPY` with `ws` through the curl tool, and a unit test pins it.
- [x] Each stand-in has a unit test that fails without it.
- [x] Cases 447, 2349 and 2300 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or have their first difference in Notes and a follow-up task.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage.

## Notes

- Decided in ADR-0735 (Claude under Stewart's delegation). `CreateRunner` takes `webSocketRandomSource` and `fileSystem`, both off by default; the harness passes `WebSocketRandomSourceFor` (2300) and, under `CURL_UPLOAD_SIZE`, `UploadSizeCappingFileSystem` (447). Upstream's `lib/sendf.c` makes `CURL_DBG_SUPPRESS_CONNECT_HDS` the same check as `--suppress-connect-headers`, so `RunCurlAsync` appends that option under it (2349).
- Touches widened to `UpstreamConformanceTests.cs` (`RunCurlAsync` passes the stand-ins) and `UpstreamConformanceTests.DebugStandIns.cs` (one rule line); no task in Doing on `origin/work/dark-factory` named either.
- The existing ws + `CURL_ENTROPY` rule already gave 2300 Debug; `PlatformFor_ToolTransferDebugVariable_ReportsDebugOnlyThen` pins it with the other two.
- `CreateRunner` went to complexity 12 with the new `??`; the HSTS clock ternary and the file-system fallback moved to `HonouredHstsClockOf` and `RunFileSystemOf`. Measure-CodeQuality -Library Curl.Console: 100% line, 100% branch, 0 failing.
- All three cases pass under `CURL_UPSTREAM_CASE` on Windows and in the fast ratchet run; none depends on the platform (no paths, OS error text or certificates in their checks).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: cee4fab0 ca97a32f 3b310db3 9495a595.
- 2026-10-21: Doing -> Done. Upstream cases 447, 2349 and 2300 pass through the harness's CURL_UPLOAD_SIZE, CURL_DBG_SUPPRESS_CONNECT_HDS and tool CURL_ENTROPY stand-ins and are on the ratchet
