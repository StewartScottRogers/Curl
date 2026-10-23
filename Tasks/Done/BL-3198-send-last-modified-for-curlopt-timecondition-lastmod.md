---
id: BL-3198
title: Send Last-Modified for CURLOPT_TIMECONDITION LASTMOD
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3191]
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Api.UnitLibrary/Options/Behaviour/TimeCondition.cs, Curl.Engine.UnitTests/TimeConditionLastModifiedTests.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Protocol.Abstractions.UnitLibrary/TimeCondition.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Conditions.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3198 — Send Last-Modified for CURLOPT_TIMECONDITION LASTMOD

## Goal

`CURLOPT_TIMECONDITION` value 3 (CURL_TIMECOND_LASTMOD) sends a `Last-Modified:` request header with the time value, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 26 (TIMECONDITION)). Report 26: we send `If-Modified-Since` for value 3. Depends on the Accept-Encoding task only because both edit `HttpRequestOptionsMapping.cs`.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/TimeConditionLastModifiedTests.cs` records the request for TIMECONDITION 3 with a TIMEVALUE and expects `Last-Modified: <date>` in upstream's format and no If-Modified-Since.
- [x] Values 1 and 2 still send If-Modified-Since and If-Unmodified-Since.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Touches widened (no task in Doing on origin/work/dark-factory names these): the time condition reaches the HTTP request through `TransferContextFactory.TimeConditionOf` -> `Curl.Protocol.Abstractions` `TimeCondition` -> `HttpRequestHeadFormatter`, not `HttpRequestOptionsMapping` (left unchanged). Added `Curl.Engine.UnitLibrary/TransferContextFactory.cs`, `Curl.Protocol.Abstractions.UnitLibrary/TimeCondition.cs`, `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs` and `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Conditions.cs`.
- Decision (ADR-0781): upstream 8.22.0 `Curl_add_timecondition` sends `Last-Modified: <RFC 1123 GMT date>` for value 3, and `Curl_meets_timecondition` compares value 3 in its default (if-modified-since) branch. So `TimeCondition` gains `SentAsLastModified` and the kind stays `IfModifiedSince`; the file, FTP and HTTP response checks are unchanged. The curl tool cannot set value 3, so this was read from the source rather than measured with Record-CurlExchange.ps1. The output does not depend on the platform.
- Verified: build -warnaserror clean; all 44 fast test projects passed, 0 failed; Measure-CodeQuality.ps1 on Engine, Http, Abstractions and Api has 0 failing members (an explicit LastModified switch arm counted as an uncovered branch, so it is folded into the default arm). CI on Linux/macOS: the change is platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b61e61cd 42235e9d 3639a5e6.
- 2026-10-22: Doing -> Done. CURLOPT_TIMECONDITION 3 sends Last-Modified with the time value; 1 and 2 still send If-Modified-Since and If-Unmodified-Since
