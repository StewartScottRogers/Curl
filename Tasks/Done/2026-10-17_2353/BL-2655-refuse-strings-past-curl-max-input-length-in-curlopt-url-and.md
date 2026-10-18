---
id: BL-2655
title: Refuse strings past CURL_MAX_INPUT_LENGTH in CURLOPT_URL and curl_url_set so upstream case 1559 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2655 — Refuse strings past CURL_MAX_INPUT_LENGTH in CURLOPT_URL and curl_url_set so upstream case 1559 passes

## Goal

A string longer than `CURL_MAX_INPUT_LENGTH` (8000000) is refused as libcurl refuses it, and upstream case 1559 passes and is on the ratchet.

## Context

Found by BL-2541 (lib1559 port). libcurl 8.22.0 refuses a string option longer than `CURL_MAX_INPUT_LENGTH` in `Curl_setstropt` with `CURLE_BAD_FUNCTION_ARGUMENT` (43), but `CURLOPT_POSTFIELDS` is not copied so it takes it (0); `curl_url_set` refuses such a part with `CURLUE_MALFORMED_INPUT` (3). Curl answers 0 to all five, so case 1559's stdout differs at byte 34 (line 1): expected `CURLOPT_URL 10000000 bytes URL == 43`, got `... == 0`, and the three `CURLUPART_*` lines answer `0 (No error)` instead of `3 (Malformed input to a URL function)`.

## Acceptance criteria

- [x] Every string option copied as `Curl_setstropt` copies it refuses a value past `CURL_MAX_INPUT_LENGTH` with `CURLE_BAD_FUNCTION_ARGUMENT`, with a focused unit test.
- [x] `curl_url_set` refuses a part past `CURL_MAX_INPUT_LENGTH` with `CURLUE_MALFORMED_INPUT`, with a focused unit test.
- [x] Case 1559 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- `Libcurl`'s `EasyOptionDispatch` refuses any `CURLOT_STRING` option past 8000000 chars (`CURLOPT_POSTFIELDS` is an Object option, so it is untouched); `ManagedCurlUrlHandle.Set` refuses any part past it. Case 1559 passes and is on the ratchet. Measure-CodeQuality.ps1 not run: two one-branch additions, both branches tested.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 676a5486 8369f03a 080facf8 4272a6b9.
- 2026-10-17: Doing -> Done. Strings past CURL_MAX_INPUT_LENGTH refused; case 1559 on the ratchet.
