---
id: BL-3290
title: curl_url_get CURLUPART_PORT with CURLU_DEFAULT_PORT returns 0 for a file URL, as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3290 — curl_url_get CURLUPART_PORT with CURLU_DEFAULT_PORT returns 0 for a file URL, as libcurl does

## Goal

curl_url_get(CURLUPART_PORT, CURLU_DEFAULT_PORT) on a file URL returns CURLUE_OK and "0", as libcurl 8.22.0 does.

## Context

Scenarios 744 to 806 (file:///c:/x, file://localhost/x, file:/x, file:///x/y?z#w, file:///, file:////h/s): Curl returns 15 (CURLUE_NO_PORT) for get flags 1 and 65; libcurl returns 0 and "0". Found by the BL-3236 differential replay of the tester's scen.json against libcurl 8.22.0 (Git for Windows ucrt64) on 2026-10-23; rerun with CURL_DIFFERENTIAL_REFERENCE_LIBCURL set (Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, ADR-0790). Measure the platform libcurl before pinning.

## Acceptance criteria

- [ ] A ManagedCurlUrlHandle unit test pins "0" for a file URL with CURLU_DEFAULT_PORT.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
