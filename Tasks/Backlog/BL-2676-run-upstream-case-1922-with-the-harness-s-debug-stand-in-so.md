---
id: BL-2676
title: Run upstream case 1922 with the harness's Debug stand-in so CURL_HSTS_HTTP reaches the libtest engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2676 — Run upstream case 1922 with the harness's Debug stand-in so CURL_HSTS_HTTP reaches the libtest engine

## Goal

Upstream case 1922 runs (not skipped) through the case runner and, when it passes on Windows, holds the ratchet.

## Context

Found by BL-2546, which ported lib1922 (`Curl.Conformance.LibTest.UnitLibrary/Lib1922.cs`: a GET of `http://hsts.example.com:<port>/1922` with HSTS enabled, `curl_easy_duphandle`, and the copy through the http-proxy, which the copied HSTS cache must upgrade to HTTPS so the proxy's refused CONNECT ends it with `CURLE_COULDNT_CONNECT`). test1922 is skipped: `test1922 is skipped: Curl lacks the feature Debug`. The case sets `CURL_HSTS_HTTP=yes`, which upstream's debug build honours to store an `http` origin's `Strict-Transport-Security`. BL-2250 added the opt-in seam for the curl tool and `UpstreamConformanceTests.cs` (around line 405) stands in for Debug for the HSTS-clock cases; extend the stand-in to this libtest case and make sure the engine the libtest runner hands the program honours `CURL_HSTS_HTTP` (and that `curl_easy_duphandle` copies the live HSTS cache; BL-2672 covers a related cache bug in lib1915), finding the project at fault and adding it to `touches` before any edit.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=1922` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` runs (does not skip) test1922; if it passes on Windows it is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, else it is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
