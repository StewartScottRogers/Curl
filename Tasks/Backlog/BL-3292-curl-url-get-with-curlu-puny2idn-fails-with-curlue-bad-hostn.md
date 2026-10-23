---
id: BL-3292
title: curl_url_get with CURLU_PUNY2IDN fails with CURLUE_BAD_HOSTNAME on a host label over 63 characters, as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3292 — curl_url_get with CURLU_PUNY2IDN fails with CURLUE_BAD_HOSTNAME on a host label over 63 characters, as libcurl does

## Goal

curl_url_get(CURLUPART_URL or CURLUPART_HOST, CURLU_PUNY2IDN) on a host with a label longer than 63 characters returns the error libcurl 8.22.0 returns (21) and no value.

## Context

Scenarios 1024-1044 (http:// plus 255 a's): Curl returns 0 and the host; libcurl returns 21 for get flag 8192 on parts 0 and 5. Check whether Windows curl (WinIDN) agrees before pinning; libcurl's IDN library decides. Found by the BL-3236 differential replay of the tester's scen.json against libcurl 8.22.0 (Git for Windows ucrt64) on 2026-10-23; rerun with CURL_DIFFERENTIAL_REFERENCE_LIBCURL set (Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, ADR-0790). Measure the platform libcurl before pinning.

## Acceptance criteria

- [ ] A ManagedCurlUrlHandle unit test pins the code for a 255-character label.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
