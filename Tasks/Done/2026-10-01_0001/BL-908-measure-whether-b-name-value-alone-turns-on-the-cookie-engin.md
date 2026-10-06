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
completed: 2026-10-01
---
# BL-908 — Measure whether -b name=value alone turns on the cookie engine and align FR-098 with it

## Goal

FR-098 and `Curl.Console/CookieEngine.cs` agree, by a measurement of real curl 8.21.0, on whether a `-b name=value` string alone (no `-b` file, no `-c`) turns on the cookie engine so a `Set-Cookie` received on the first of two URLs is sent on the second, and `-L -b ""` through a redirect is pinned by a test.

## Context

- Found by BL-665 (2026-09-29): FR-098 says "any `-b`" turns on the cookie engine. `CookieEngine` turns it on only for a `-b` file name (`-b ""` is one) or `-c`. BL-237's Notes and `CurlCommandRunnerCookieTests.RunAsync_CookieStringOnlyForTwoUrls_NeverSendsTheReceivedCookie` side with the code; the row contradicts them.
- Measure with `Record-CurlExchange.ps1` (two URLs, first answers `Set-Cookie`), with `-b a=b` alone, with `-b ""`, and with `-L -b ""` across a redirect.

## Acceptance criteria

- [x] The measurement (command lines and whether the second request carries the cookie) is recorded under Notes.
- [x] FR-098's wording matches the measurement, and `CookieEngine` behaves as measured, shown by a test in `CurlCommandRunnerCookieTests`.
- [x] A test drives `-L -b ""` through a redirect and pins whether the cookie set on the first hop is sent on the second.

## Notes

- Measured 2026-10-01 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`, the first answer `HTTP/1.1 200 OK` (or `302 Found`, `Location: /b`) with `Set-Cookie: s=v`, the second an empty 200:
  - `curl -s -b a=b http://127.0.0.1:P/a http://127.0.0.1:P/b`: both requests carry `Cookie: a=b` only; `s=v` is never sent.
  - `curl -s -b "" http://127.0.0.1:P/a http://127.0.0.1:P/b`: the first carries no `Cookie`, the second `Cookie: s=v`.
  - `curl -s -L -b "" http://127.0.0.1:P/a` through the 302: the second hop carries `Cookie: s=v`.
  - `curl -s -L -b a=b http://127.0.0.1:P/a` through the 302: both hops carry `Cookie: a=b` only.
  - `curl -s -L http://127.0.0.1:P/a` through the 302: neither hop carries `Cookie`.
- So `-b name=value` alone does not turn on the cookie engine; a `-b` file name (`-b ""` included) or `-c` does. `CookieEngine` already behaved so; no production change. FR-098's "any `-b`" was wrong and is reworded.
- Tests added in `CurlCommandRunnerCookieTests`: `RunAsync_EmptyCookieFileNameForTwoUrls_SendsTheReceivedCookieOnTheSecond`, `RunAsync_FollowedRedirectWithEmptyCookieFileName_SendsTheCookieSetByTheFirstHop`, `RunAsync_FollowedRedirectWithCookieStringOnly_NeverSendsTheCookieSetByTheFirstHop` (the existing `RunAsync_CookieStringOnlyForTwoUrls_NeverSendsTheReceivedCookie` covers `-b a=b` with two URLs).
- Pipeline `feature` collapsed to measure, test, document: the code already matched curl, so there was nothing to plan or implement. No ADR: measurement settled it.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Measured: -b name=value alone keeps the cookie engine off, -b "" turns it on; FR-098 reworded and -L -b "" redirect pinned by tests
