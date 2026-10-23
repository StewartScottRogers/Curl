---
id: BL-3198
title: Send Last-Modified for CURLOPT_TIMECONDITION LASTMOD
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3191]
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Api.UnitLibrary/Options/Behaviour/TimeCondition.cs, Curl.Engine.UnitTests/TimeConditionLastModifiedTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3198 — Send Last-Modified for CURLOPT_TIMECONDITION LASTMOD

## Goal

`CURLOPT_TIMECONDITION` value 3 (CURL_TIMECOND_LASTMOD) sends a `Last-Modified:` request header with the time value, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 26 (TIMECONDITION)). Report 26: we send `If-Modified-Since` for value 3. Depends on the Accept-Encoding task only because both edit `HttpRequestOptionsMapping.cs`.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/TimeConditionLastModifiedTests.cs` records the request for TIMECONDITION 3 with a TIMEVALUE and expects `Last-Modified: <date>` in upstream's format and no If-Modified-Since.
- [ ] Values 1 and 2 still send If-Modified-Since and If-Unmodified-Since.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b61e61cd 42235e9d 3639a5e6.
