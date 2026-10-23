---
id: BL-3297
title: curl_url_get CURLUPART_URL returns file://(nil) for a file URL with no path, as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3297 — curl_url_get CURLUPART_URL returns file://(nil) for a file URL with no path, as libcurl does

## Goal

A URL handle whose scheme is set to file with no path gets CURLUPART_URL "file://(nil)", as libcurl 8.22.0 does.

## Context

Scenarios 1730-1733, 2023-2026, 2902-2905 (set CURLUPART_SCHEME "file" on an empty handle, http://a/ or http://[::1]/) and 2769-2772 (clear the path of file:///x): Curl gives file:///, libcurl gives file://(nil) (its printf of a NULL path). Found by the BL-3236 differential replay of the tester's scen.json against libcurl 8.22.0 (Git for Windows ucrt64) on 2026-10-23; rerun with CURL_DIFFERENTIAL_REFERENCE_LIBCURL set (Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, ADR-0790). Measure the platform libcurl before pinning.

## Acceptance criteria

- [ ] A ManagedCurlUrlHandle unit test pins file://(nil) for both scenarios.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b40ffbeb c46ca32a.
