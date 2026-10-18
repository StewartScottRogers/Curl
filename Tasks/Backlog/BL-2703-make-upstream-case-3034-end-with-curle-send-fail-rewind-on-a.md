---
id: BL-2703
title: Make upstream case 3034 end with CURLE_SEND_FAIL_REWIND on a redirected upload without a seek callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2703 — Make upstream case 3034 end with CURLE_SEND_FAIL_REWIND on a redirected upload without a seek callback

## Goal

Upstream case 3034 (lib3034) passes through the runner and is on the ratchet.

## Context

Found by BL-2551, which ported lib3034 (`Curl.Conformance.LibTest.UnitLibrary/Lib3034.cs`): a verbose upload (`CURLOPT_UPLOAD`, `CURLOPT_INFILESIZE` 5, a `CURLOPT_READFUNCTION` and no `CURLOPT_SEEKFUNCTION`) with `CURLOPT_FOLLOWLOCATION`; the server answers `307` with `Location: /3034`, so real curl cannot rewind the body and ends the first transfer with `CURLE_SEND_FAIL_REWIND` (65); after `curl_easy_reset` a plain GET of the same URL succeeds and the program ends with 0. Curl hangs: `test3034 fails: curl did not finish within 20 seconds`. Find whether the first transfer or the transfer after the reset hangs (the engine's follow of a redirected upload with no seek callback, or a reset handle still holding the upload state), add the project at fault to `touches` before any edit, and run with `CURL_UPSTREAM_CASE=3034` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [ ] Case 3034 passes through the runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
