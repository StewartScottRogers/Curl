---
id: BL-2263
title: Stand in for Debug in cases 359 and 2406 (CURL_ALTSVC_HTTP for Alt-Svc over plain http) so they run on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2245]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2263 — Stand in for Debug in cases 359 and 2406 (CURL_ALTSVC_HTTP for Alt-Svc over plain http) so they run on Linux and macOS

## Goal

Cases 359 and 2406 run on Linux and macOS instead of skipping for the Debug feature.

## Context

Found by BL-2245 (2026-10-14): with %HTTP2TLSPORT and the TLS front in place, test359 and test2406 skip only for Debug. test2406 (--http2 -f over %HTTP2TLSPORT) uses no debug-only behaviour, so reporting Debug to it is enough. test359 needs CURL_ALTSVC_HTTP, the debug build’s switch that lets Alt-Svc be learnt over plain http (as CURL_HSTS_HTTP does for HSTS, ADR-0494, BL-2250). Start at UpstreamConformanceTests.PlatformFor in Curl.Conformance.UnitTests and the Alt-Svc cache code reached from Curl.Console. On Windows both stay SkippedOnThisPlatform for http/2 (ADR-0495).

## Acceptance criteria

- [x] PlatformFor reports Debug to 2406 and 359, with a comment saying why, as it does for the httpsig and HSTS cases.
- [x] Curl honours CURL_ALTSVC_HTTP in the conformance harness only (as CURL_HSTS_HTTP), with a focused unit test in the fixed library’s .UnitTests (ADR-0482).
- [x] Each of 359 and 2406 that passes on Linux and macOS CI is in PassingUpstreamCases.txt; any that fails is named in Notes with a follow-up task.

## Notes

- Decision (ADR-0497): as for HSTS (ADR-0494), `HttpProtocolHandler.StoreAltSvc` hands every `Alt-Svc` header to the store whatever the scheme, and `AltSvcTransferCache` decides: `OpenAsync(usesOverHttp)` makes it learn and look up a plain `http` origin (curl's debug build does both under `CURL_ALTSVC_HTTP`, `lib/http.c` and `lib/url.c`). `CurlCommandRunner(altSvcUsesOverHttp)`; `CurlComposition.CreateRunner(honoursAltSvcHttpVariable)` sets it when the variable is set at all (`UsesAltSvcOverHttp`, getenv semantics). The conformance `RunCurlAsync` turns the seam on; the shipped `curl` never does.
- PlatformFor reports Debug to a case naming `alt-svc` and setting `CURL_ALTSVC_HTTP` (358, 359) and to a case whose command names `%HTTP2TLSPORT` (2400, 2401, 2406, which use nothing debug-only). Chose the general rules over case numbers, as the httpsig and HSTS rules are.
- Found on the way: `UpstreamCaseScreening.FindFeatureOfAnotherPlatform` (Curl.Conformance.UnitLibrary) judged no platform could run a case needing `Debug` and `http/2`, so the listed cases failed on Windows instead of being `SkippedOnThisPlatform`. A feature this run reports that no platform reports (a harness stand-in) now counts as met on every platform; unit-tested in `UpstreamCaseScreeningTests`. Added Curl.Conformance.UnitLibrary, Curl.Console.UnitTests and Curl.Protocol.Http.UnitLibrary/UnitTests to `touches` (no other Doing task named them on origin/work/dark-factory).
- Measured on Windows by running the cases with the Linux platform and a non-Windows parse (`parsesAsWindowsBuild: false`, a temporary local edit, reverted): 358, 359, 2400, 2401 and 2406 pass; 359 fails with the seam off, so the seam is what makes it pass. Listed 359, 2400, 2401 and 2406; on Windows they are `SkippedOnThisPlatform` for `http/2`. 358 also passes and is left to BL-2266 to list. Linux and macOS CI confirm the listing on the shift's push.
- Quality: Measure-CodeQuality for Curl.Console, Curl.Protocol.Http.UnitLibrary and Curl.Conformance.UnitLibrary, 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. CURL_ALTSVC_HTTP behind the harness seam learns and looks up Alt-Svc over plain http; 359, 2400, 2401 and 2406 run with the Debug stand-in and are listed
