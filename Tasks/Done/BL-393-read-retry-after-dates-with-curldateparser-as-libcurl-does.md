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
completed: 2026-09-27
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

- [x] `RetryAfterHeader.ParseSeconds` reads a date through `CurlDateParser`; the `HttpDateFormats` table is gone.
- [x] `RetryAfterHeaderTests` pins at least two non-RFC-9110 dates measured on curl 8.21.0, and the existing cases still pass.
- [x] A new ADR supersedes ADR-0080, and ADR-0080 and the Decisions README index are marked `Superseded by` it.
- [x] Curl.Core.UnitLibrary stays at 100% line and branch coverage.

## Notes

- Measured 2026-09-27 with curl 8.21.0 (`/mingw64/bin/curl`, mingw, Schannel) through `Record-CurlExchange.ps1 -Connections 2 -Response 'HTTP/1.1 503 Service Unavailable\r\nRetry-After: <date>\r\nContent-Length: 0\r\n\r\n' -CurlArgs '--retry','1','-o','NUL',<url>`, each date four seconds ahead of UTC now, reading the stderr warning: `27 Sep 2026 20:37:58`, `Sep 27 2026 20:38:02` (3, second boundary), `20260927 20:38:06`, `Sun, 27 Sep 2026 20:38:10 +0000`, `2026 Sep 27 20:38:14 UTC`, `27 Sep 2026 21:39:00 +0100` all gave `Retrying in 4 seconds`; `5 Sep` gave 5 (no year, so delay-seconds); `1 Jan 2000` gave the 1-second backoff. Pinned in `ParseSeconds_LenientDate_ReadsAsCurlGetdateDoes`, shifted to the test's fixed `Now`.
- Seconds are `unixSeconds - now.ToUnixTimeSeconds()`, curl's `date - time(NULL)`; the existing RFC 9110 cases pass unchanged.
- ADR-0094 records the decision and supersedes ADR-0080. `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. RetryAfterHeader reads Retry-After dates with CurlDateParser, so lenient dates curl 8.21.0 reads wait as curl waits (ADR-0094)
