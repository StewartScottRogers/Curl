---
id: BL-1448
title: Count response headers across redirect hops, trailers and CONNECT heads toward curl's 5000 limit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1431]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-067
created: 2026-10-04
completed:
---
# BL-1448 — Count response headers across redirect hops, trailers and CONNECT heads toward curl's 5000 limit

## Goal

curl 8.21.0's 5000-header limit (`curl: (100) Too many response headers, 5000 is max`) counts every header the transfer stores - the hops of a followed redirect, chunked trailers and CONNECT reply heads - not only the heads of one exchange.

## Context

- BL-1431 added the limit in `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` (`MaximumHeaderCount`), counting a single exchange's 1xx heads and final head, which one `HttpResponseHeadReader` reads.
- Upstream (tag `curl-8_21_0`): `lib/headers.c` `Curl_headers_push` refuses a header once 5000 are stored; the list is cleared only by `Curl_headers_cleanup` in `Curl_pretransfer` (`lib/transfer.c` line 601), so a `-L` redirect's hops add up, and trailers (`CLIENTWRITE_TRAILER`) and CONNECT heads are pushed too. Measure each with `Record-CurlExchange.ps1` (it serves one response per connection; a redirect's hops can each carry 3000 headers) before pinning behaviour, since the -v and -i bytes around the failure must match.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` pins the measured exit code, header output and last `<` line for a `-L` redirect whose two hops carry 3000 headers each.
- [ ] A test pins the measured behaviour for chunked trailers that take the count past 5000, or Notes record the measurement showing curl does not count them.
- [ ] `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
