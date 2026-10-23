---
id: BL-3293
title: curl_url_get with CURLU_URLDECODE keeps decoded bytes that are not UTF-8 instead of U+FFFD
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Url/CurlUrlHandle.cs, Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3293 — curl_url_get with CURLU_URLDECODE keeps decoded bytes that are not UTF-8 instead of U+FFFD

## Goal

curl_url_get with CURLU_URLDECODE returns the decoded bytes even when they are not UTF-8 (%FF gives 0xFF), as libcurl 8.22.0 does.

## Context

Scenarios 1080-1100 (http://a/%FF, %C0%AF, %ED%A0%80): Curl's CurlUrlHandle.Get returns a string, so the decoded bytes come back as U+FFFD (EF BF BD); libcurl returns the raw bytes. The managed API needs a byte form or a lossless mapping; decide in an ADR. Found by the BL-3236 differential replay of the tester's scen.json against libcurl 8.22.0 (Git for Windows ucrt64) on 2026-10-23; rerun with CURL_DIFFERENTIAL_REFERENCE_LIBCURL set (Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, ADR-0790). Measure the platform libcurl before pinning.

## Acceptance criteria

- [ ] A unit test pins the raw bytes for http://a/%FF with CURLU_URLDECODE through the API the C ABI uses.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
