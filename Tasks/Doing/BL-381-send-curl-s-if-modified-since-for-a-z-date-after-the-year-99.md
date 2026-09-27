---
id: BL-381
title: Send curl's If-Modified-Since for a -z date after the year 9999
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-247]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-009
created: 2026-09-27
completed:
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

- [ ] The measured curl 8.21.0 request header (or exit code and error text) for
      `-z "1 Jan 099999999"` and `-z "1 Jan 3001"` over HTTP is recorded in `Notes`.
- [ ] A test in `Curl.Protocol.Http.UnitTests` pins the same bytes (or exit code) for a
      `TimeCondition` whose value is 9999-12-31 23:59:59 UTC, or `Notes` records why the
      clamped header already matches.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed from BL-247. If curl's behaviour depends on the real year (3001 vs 99999999), this needs
  `TimeCondition` widened; record that in an ADR superseding ADR-0073.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
