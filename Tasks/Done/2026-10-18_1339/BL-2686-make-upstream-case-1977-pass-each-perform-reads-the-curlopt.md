---
id: BL-2686
title: Make upstream case 1977 pass: each perform reads the CURLOPT_CURLU handle's current URL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2628]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2686 — Make upstream case 1977 pass: each perform reads the CURLOPT_CURLU handle's current URL

## Goal

Upstream case 1977 passes through the case runner and holds the ratchet.

## Context

Found by BL-2548, which ported lib1977 as `Lib1977` in `Curl.Conformance.LibTest.UnitLibrary` (unit tested over a fake engine). The program performs three times over `CURLOPT_CURLU`: a first handle, a second handle with the query `foo`, then the second handle with `bar` appended (`CURLU_APPENDQUERY`) and not set again, printing `effective URL: ...` after each. Difference: `<verify><protocol> differs at byte 0 (line 1): expected "GET /1977 HTTP/1.1\r\n", got the end`; the first perform returns 3 (`CURLE_URL_MALFORMAT`) although the handle's URL is `http://127.0.0.1:8990/1977`: Libcurl stores the handle in `EasyNetworkOptions.UrlHandle`, but nothing in the engine reads it. BL-2628 makes a transfer use the `CURLOPT_CURLU` handle; once it is Done, check that each perform reads the handle's current contents (the third transfer) and that `CURLINFO_EFFECTIVE_URL` answers that URL. Run it with `CURL_UPSTREAM_CASE=1977` and the manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; add the engine project at fault to `touches` before any edit.

## Acceptance criteria

- [x] Upstream case 1977 passes through the case runner on Windows and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] Any fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-18: Measured first, before any edit: with BL-2628 Done, `CURL_UPSTREAM_CASE=1977` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` already pass (all three performs, including the third that reads the `CURLU_APPENDQUERY` change without a fresh `CURLOPT_CURLU`). No product fix was needed, so no new unit test (ADR-0482 applies to fixes only) and no engine project joined `touches`. Added 1977 to `PassingUpstreamCases.txt`. The case is HTTP over loopback with no paths, file system or OS error text, so its output does not depend on the platform.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: f49fc50e 2d55fa0b 4272a6b9 8738c7f6.
- 2026-10-18: Doing -> Done. Upstream case 1977 passes and holds the ratchet: each perform reads the CURLOPT_CURLU handle's current URL
