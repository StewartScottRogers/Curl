---
id: BL-2676
title: Run upstream case 1922 with the harness's Debug stand-in so CURL_HSTS_HTTP reaches the libtest engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2676 — Run upstream case 1922 with the harness's Debug stand-in so CURL_HSTS_HTTP reaches the libtest engine

## Goal

Upstream case 1922 runs (not skipped) through the case runner and, when it passes on Windows, holds the ratchet.

## Context

Found by BL-2546, which ported lib1922 (`Curl.Conformance.LibTest.UnitLibrary/Lib1922.cs`: a GET of `http://hsts.example.com:<port>/1922` with HSTS enabled, `curl_easy_duphandle`, and the copy through the http-proxy, which the copied HSTS cache must upgrade to HTTPS so the proxy's refused CONNECT ends it with `CURLE_COULDNT_CONNECT`). test1922 is skipped: `test1922 is skipped: Curl lacks the feature Debug`. The case sets `CURL_HSTS_HTTP=yes`, which upstream's debug build honours to store an `http` origin's `Strict-Transport-Security`. BL-2250 added the opt-in seam for the curl tool and `UpstreamConformanceTests.cs` (around line 405) stands in for Debug for the HSTS-clock cases; extend the stand-in to this libtest case and make sure the engine the libtest runner hands the program honours `CURL_HSTS_HTTP` (and that `curl_easy_duphandle` copies the live HSTS cache; BL-2672 covers a related cache bug in lib1915), finding the project at fault and adding it to `touches` before any edit.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=1922` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` runs (does not skip) test1922; if it passes on Windows it is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, else it is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 2, first pass, no code edited): the project at fault is `Curl.Engine.UnitLibrary`.
  `ManagedEngineSharedState` builds its HSTS cache as `new HstsTransferPolicy(timeProvider)`, so the
  libtest engine never learns `Strict-Transport-Security` over plain http; `HstsTransferPolicy` already
  takes `learnsOverHttp` (ADR-0494), it only needs a seam. Plan: give `ManagedCurlEngine`'s internal
  constructor a `learnsHstsOverHttp` flag passed to `ManagedEngineSharedState` and on to
  `HstsTransferPolicy`, with a focused test in `Curl.Engine.UnitTests` (an http answer carrying
  `Strict-Transport-Security` switches a later http URL of the same host to https only with the flag);
  then `UpstreamConformanceTests.CreateEngine` passes
  `invocation.EnvironmentVariables.ContainsKey("CURL_HSTS_HTTP")`, and `PlatformFor` reports Debug to a
  case naming HSTS and setting `CURL_HSTS_HTTP=`. Nothing else is missing: `curl_easy_duphandle` needs no
  change, as the HSTS cache is engine-wide (`ManagedEngineSharedState.Hsts`), and a refused CONNECT already
  ends with exit 7 (`TcpConnector.cs`, `CONNECT tunnel failed, response <code>`).
- Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches` for that seam. BL-2734, in
  Doing, touches both, so this task goes back to Backlog until BL-2734 is done.

- 2026-10-19 (lane 1): `ManagedCurlEngine` passes `LearnsHstsOverHttp(readEnvironmentVariable)` (only when a reader is given, as the `CURL_ALTSVC_HTTP` seam does) to `ManagedEngineSharedState`, whose HSTS cache then learns over http; `PlatformFor` reports Debug to a case naming HSTS and setting `CURL_HSTS_HTTP` (only test1922 newly gains it; 446 and 780-783 already had it through `CURL_TIME`). Case 1922 then ran and failed on `Dup effective URL: http://...`: the dup did CONNECT to 443, but `Info.EffectiveUrl` fell back to the URL set when the failed connect left no report URL. `FinishPerformed` now takes the URL the setup chose (the HSTS-switched one). No ADR: this extends ADR-0494's stand-in to the libtest engine. test1922 passes on Windows and is on the ratchet; it touches no path, OS error or certificate. Measure-CodeQuality on Curl.Engine.UnitLibrary: 0 failing members. Touches left whole-project (step 2a narrowing skipped: lanes may not push).

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: c007b8bb c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs Curl.Engine.UnitLibrary and Curl.Engine.UnitTests (a CURL_HSTS_HTTP seam on ManagedCurlEngine), which BL-2734 in Doing touches
- 2026-10-19: Backlog -> Doing. Criteria at claim: c007b8bb c59fcab3 3d836d5e.
- 2026-10-19: Doing -> Done. upstream case 1922 runs and passes: the libtest engine learns HSTS over http under CURL_HSTS_HTTP and reports the HSTS-switched effective URL
