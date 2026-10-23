---
id: BL-3194
title: Fix CONTENT_LENGTH_DOWNLOAD_T, CONDITION_UNMET, EFFECTIVE_URL with CURLOPT_PORT and file:// getinfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3193]
touches: [Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Curl.Engine.UnitTests/TransferInfoLengthAndConditionTests.cs, Libcurl.UnitTests/Info/EasyInfoLengthAndConditionTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3194 — Fix CONTENT_LENGTH_DOWNLOAD_T, CONDITION_UNMET, EFFECTIVE_URL with CURLOPT_PORT and file:// getinfo

## Goal

`CONTENT_LENGTH_DOWNLOAD_T`, `CONDITION_UNMET`, `EFFECTIVE_URL` and `FILETIME` report what upstream reports in the cases below.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 23 (lengths, condition, url, file)). From report 23: CONDITION_UNMET is 0 for a plain 304 without a time condition; upstream says 1. CONTENT_LENGTH_DOWNLOAD_T should be -1 for a chunked response, when Content-Length and Transfer-Encoding conflict, for duplicate differing lengths, and under FAILONERROR; 0 for a 204 and for a 304 that sends a length. EFFECTIVE_URL should not gain `:18080` when CURLOPT_PORT is set. FILETIME and CONTENT_LENGTH_DOWNLOAD_T should be filled in for file://. Depends on the auth/referer getinfo task only because both edit `TransferInfoFromReport.cs`.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] Tests pin CONTENT_LENGTH_DOWNLOAD_T for chunked, conflicting CL+TE, duplicate differing CL, FAILONERROR, 204 and 304-with-length to upstream's values.
- [ ] A test shows CONDITION_UNMET is 1 for a plain 304 with no time condition.
- [ ] A test shows EFFECTIVE_URL keeps the URL's own form when CURLOPT_PORT=18080 is set.
- [ ] A test shows FILETIME (with CURLOPT_FILETIME) and CONTENT_LENGTH_DOWNLOAD_T for a file:// transfer match upstream (platform-neutral path).
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
