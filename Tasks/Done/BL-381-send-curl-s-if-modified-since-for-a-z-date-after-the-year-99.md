---
id: BL-381
title: Send curl's If-Modified-Since for a -z date after the year 9999
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-247]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: FR-009
created: 2026-09-27
completed: 2026-09-27
---
# BL-381 — Send curl's If-Modified-Since for a -z date after the year 9999

## Goal

An HTTP transfer with `-z "1 Jan 099999999"` sends what curl 8.21.0 sends (or fails as it
fails), instead of `If-Modified-Since: Fri, 31 Dec 9999 23:59:59 GMT`.

## Context

- BL-247 / ADR-0073: `CurlDateParser` reads a `-z` date after the year 9999 as
  9999-12-31 23:59:59 UTC, because `TimeCondition.Value` is a `DateTimeOffset`. That is exact for
  every comparison against a file or `Last-Modified` time, but the HTTP request header shows the
  clamped date.
- curl formats the header from its 64-bit `time_t` with `Curl_gmtime`; on Windows `gmtime_s`
  refuses years past 3000, so curl may send something else or fail the transfer. Not measured.
- Start at `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs` (`AppendTimeCondition`).
- Measure with curl 8.21.0 (Windows) against a local listener: `curl -v -z "1 Jan 099999999"`,
  and `-z "1 Jan 3001"` to find where the header changes.

## Acceptance criteria

- [x] The measured curl 8.21.0 request header (or exit code and error text) for
      `-z "1 Jan 099999999"` and `-z "1 Jan 3001"` over HTTP is recorded in `Notes`.
- [x] A test in `Curl.Protocol.Http.UnitTests` pins the same bytes (or exit code) for a
      `TimeCondition` whose value is 9999-12-31 23:59:59 UTC, or `Notes` records why the
      clamped header already matches.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed from BL-247. If curl's behaviour depends on the real year (3001 vs 99999999), this needs
  `TimeCondition` widened; record that in an ADR superseding ADR-0073.
- Measured 2026-09-27, curl 8.21.0 Schannel build (Git for Windows mingw64),
  `Record-CurlExchange.ps1 -CurlArgs '-sS','-z',<date>,'http://127.0.0.1:18381/'`:
  - `-z "1 Jan 3001"`: exit 0, sends `If-Modified-Since: Thu, 01 Jan 3001 00:00:00 GMT`.
  - `-z "1 Jan 099999999"`: exit 43, stderr `curl: (43) Invalid TIMEVALUE`, nothing sent.
  - Bisected: `1 Jan 3001 20:59:59 GMT` is the last time sent, `21:00:00` fails. That is Unix
    32535291599 = UCRT `_MAX__TIME64_T` + 13 h `_MAX_LOCAL_TIME`, `gmtime`'s limit, not the
    time zone. `1 Jan 4000`, `1 Jan 9999`, `31 Dec 9999 23:59:59` also exit 43.
  - `-v` shows the connect, `* Invalid TIMEVALUE`, then `Connection #0 ... left intact`.
  - `-z -<date>` (If-Unmodified-Since) and an `-H "If-Modified-Since: x"` override fail too.
- Decision (ADR-0089): on Windows, a `TimeCondition` after that moment fails the HTTP
  exchange with exit 43 before sending, connection marked reusable
  (`HttpTimeConditionLimit`, checked at the top of `ExchangeOnConnectionAsync`). Elsewhere
  unchanged. `TimeCondition` not widened: every clamped value is past the Windows limit, so
  the clamp is only visible on Linux/macOS for years > 9999 (unmeasured, stays as ADR-0073).
- Pinned by `HttpTimeConditionLimitTests` and
  `HttpProtocolHandlerTests.ExecuteAsync_TimeConditionAfterTheYear9999_FailsWith43OnWindowsAndSendsTheClampedDateElsewhere`.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0089 and its README row; no
  task in Doing names it.
- The verbose `* Invalid TIMEVALUE` line is the console's generic failure echo, not pinned here.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. On Windows an HTTP -z time past 3001-01-01 20:59:59 UTC fails with exit 43 Invalid TIMEVALUE as curl 8.21.0 does (ADR-0089)
