---
id: BL-2250
title: Honour CURL_HSTS_HTTP behind the harness seam so upstream case 446 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2250 — Honour CURL_HSTS_HTTP behind the harness seam so upstream case 446 passes

## Goal

Upstream case 446 passes in the conformance ratchet: with the harness seam on, `CURL_HSTS_HTTP` set makes Curl learn `Strict-Transport-Security` from a plain `http` response, as curl's debug build does.

## Context

Found by BL-2234, which stands in for the Debug feature of the HSTS cases that set `CURL_TIME` (ADR-0493: `CreateRunner(honoursHstsTimeVariable: true)` fixes the HSTS clock). Cases 780-783 now pass; 446 still fails: its two URLs go through the http-proxy as plain `GET http://...`, and upstream's debug build stores their `Strict-Transport-Security` headers only because `CURL_HSTS_HTTP` is set (`lib/http.c`, `#ifdef DEBUGBUILD`). Curl ignores an `http` origin's header in two places: `HttpProtocolHandler.StoreHsts` (checks `context.Url.Scheme == "https"`) and `Curl.Core`'s `HstsTransferPolicy.StoreFromResponse` (returns early for a non-`https` origin). Add an opt-in seam like `honoursHstsTimeVariable` (off in the shipped `curl`, on in `UpstreamConformanceTests.RunCurlAsync`), e.g. a flag on `HttpRequestOptions` or on the store, and record it in an ADR. Note 446's URL `http://this.hsts.example./` has a trailing dot the file must drop. Current failure: `<verify><file> hsts446 differs at byte 113 (line 3): expected "this.hsts.example \"20330525 03:33:20\"", got the end`.

## Acceptance criteria

- [x] Case 446 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and passes.
- [x] The shipped `curl` (the seam off) still ignores `Strict-Transport-Security` over `http`, pinned by a unit test; the seam on stores it, pinned by a unit test in each changed library's `.UnitTests`.
- [x] `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate; build clean; fast tests green.

## Notes

- Decision (ADR-0494): the HTTP handler now hands every `Strict-Transport-Security` header to `HttpRequestOptions.HstsStore` whatever the scheme, and the store decides, as curl's debug build runs the same `Curl_hsts_parse` (and the same `Illegal STS header skipped` line) for both. `HstsTransferPolicy(learnsOverHttp)` off by default; `CurlCommandRunner(hstsLearnsOverHttp)`; `CurlComposition.CreateRunner(honoursHstsHttpVariable)` turns it on when `CURL_HSTS_HTTP` is set at all (`LearnsHstsOverHttp`, getenv semantics). Conformance `RunCurlAsync` turns the seam on; the shipped `curl` never does.
- The trailing dot of `this.hsts.example.` was already dropped by `HstsCache`; no change needed there.
- Tests: Core `StoreFromResponse_HttpOriginWhenLearningOverHttp_StoresTheHostWithoutItsTrailingDot` (seam on) beside the existing `StoreFromResponse_HttpOrigin_ReturnsTrueAndStoresNothing` (off); Http `ExecuteAsync_HttpStsTheStoreRefuses_...`/`ExecuteAsync_HttpStsTheStoreTakes_...` replace `ExecuteAsync_HttpSts_DoesNotCallTheStore`; Console `RunAsync_HeaderOverPlainHttpWhenLearningOverHttp_...` beside `RunAsync_HeaderOverPlainHttp_IsIgnored...` (off) and `LearnsHstsOverHttp_*`. Abstractions changed in doc comments only.
- Measure-CodeQuality for Curl.Core.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Console: 0 failing members. Fast tests green (Console 2867, Http 2058, Core 1593, Conformance 2951 passed).

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. CURL_HSTS_HTTP behind the harness seam learns HSTS over plain http; upstream case 446 passes
