---
id: BL-908
title: Measure whether -b name=value alone turns on the cookie engine and align FR-098 with it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Product/Requirements.md]
requirement: FR-098
created: 2026-09-29
completed:
---
# BL-908 — Measure whether -b name=value alone turns on the cookie engine and align FR-098 with it

## Goal

FR-098 and `Curl.Console/CookieEngine.cs` agree, by a measurement of real curl 8.21.0, on whether a `-b name=value` string alone (no `-b` file, no `-c`) turns on the cookie engine so a `Set-Cookie` received on the first of two URLs is sent on the second, and `-L -b ""` through a redirect is pinned by a test.

## Context

- Found by BL-665 (2026-09-29): FR-098 says "any `-b`" turns on the cookie engine. `CookieEngine` turns it on only for a `-b` file name (`-b ""` is one) or `-c`. BL-237's Notes and `CurlCommandRunnerCookieTests.RunAsync_CookieStringOnlyForTwoUrls_NeverSendsTheReceivedCookie` side with the code; the row contradicts them.
- Measure with `Record-CurlExchange.ps1` (two URLs, first answers `Set-Cookie`), with `-b a=b` alone, with `-b ""`, and with `-L -b ""` across a redirect.

## Acceptance criteria

- [ ] The measurement (command lines and whether the second request carries the cookie) is recorded under Notes.
- [ ] FR-098's wording matches the measurement, and `CookieEngine` behaves as measured, shown by a test in `CurlCommandRunnerCookieTests`.
- [ ] A test drives `-L -b ""` through a redirect and pins whether the cookie set on the first hop is sent on the second.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
