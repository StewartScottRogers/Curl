---
id: BL-393
title: Read Retry-After dates with CurlDateParser as libcurl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-318]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-393 — Read Retry-After dates with CurlDateParser as libcurl does

## Goal

`RetryAfterHeader` reads a `Retry-After` date with `CurlDateParser`, so every date libcurl 8.21.0's `Curl_getdate_capped` reads waits as curl waits.

## Context

- ADR-0080 (BL-318): `RetryAfterHeader` reads only the three RFC 9110 HTTP-date forms with `DateTimeOffset.TryParseExact`, because porting `parsedate.c` was not worth the code.
- ADR-0074 since put that port, `Curl.Protocol.Abstractions.CurlDateParser` (`TryParse(string, out long unixSeconds)`), in a project `Curl.Core` already references, so the reason is gone.
- A non-conforming date such as `27 Sep 2026 05:26:19` is read by curl as a date and by Curl today as delay-seconds (zero, so the backoff).
- Measure curl 8.21.0 on such dates before pinning; BL-208's notes describe the loopback server used.

## Acceptance criteria

- [ ] `RetryAfterHeader.ParseSeconds` reads a date through `CurlDateParser`; the `HttpDateFormats` table is gone.
- [ ] `RetryAfterHeaderTests` pins at least two non-RFC-9110 dates measured on curl 8.21.0, and the existing cases still pass.
- [ ] A new ADR supersedes ADR-0080, and ADR-0080 and the Decisions README index are marked `Superseded by` it.
- [ ] Curl.Core.UnitLibrary stays at 100% line and branch coverage.

## Notes

## Log

- 2026-09-27: Created.
