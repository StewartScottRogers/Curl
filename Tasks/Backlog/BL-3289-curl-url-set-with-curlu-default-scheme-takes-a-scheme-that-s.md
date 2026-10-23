---
id: BL-3289
title: curl_url_set with CURLU_DEFAULT_SCHEME takes a scheme that starts with a digit or dash as a host, as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3289 — curl_url_set with CURLU_DEFAULT_SCHEME takes a scheme that starts with a digit or dash as a host, as libcurl does

## Goal

curl_url_set(CURLUPART_URL, "1ab://a" or "-ab://a", CURLU_DEFAULT_SCHEME) returns CURLUE_OK and the URL https://1ab//a, and "+ab://a" returns CURLUE 21, as libcurl 8.22.0 does.

## Context

Scenarios 591, 598, 605: Curl returns 4 for all three; libcurl returns 0 (https://1ab//a, https://-ab//a) and 21 for +ab://a. Found by the BL-3236 differential replay of the tester's scen.json against libcurl 8.22.0 (Git for Windows ucrt64) on 2026-10-23; rerun with CURL_DIFFERENTIAL_REFERENCE_LIBCURL set (Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, ADR-0790). Measure the platform libcurl before pinning.

## Acceptance criteria

- [ ] A ManagedCurlUrlHandle unit test pins the three set codes and the URL libcurl gives.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
