---
id: BL-443
title: Print curl's -v lines for Set-Cookie headers the parser refuses
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-367]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-443 — Print curl's -v lines for Set-Cookie headers the parser refuses

## Goal

Under `-v`, a `Set-Cookie` header that `SetCookieParser` refuses is reported with the `* ` line curl 8.21.0 prints for that refusal, or with nothing where curl prints nothing.

## Context

- Follow-up from BL-367, which made `CookieStore.StoreFromResponse(url, headers, now, events)` report `Added cookie ...`, `Replaced cookie ...`, the Public Suffix List drop and the secure-overlay drop through `ITransferEvents.ReportInfo`. The parser's refusals were left out because `SetCookieParser.Parse` returns only `null`, with no reason; BL-367's Notes hold the measured runs.
- Measured 2026-09-27 on curl 8.21.0 (mingw, the ADR-0009 reference) with `Record-CurlExchange.ps1` serving `Set-Cookie: <header>` to `curl -s -v --resolve www.example.co.uk:<port>:127.0.0.1 -c - http://www.example.co.uk:<port>/`; stderr lines, after `* `:
  - `n3=v; Path=/; Domain=other.test` -> `skipped cookie with bad tailmatch domain: other.test`
  - `i=1; Domain=127.0.0.2` from `http://127.0.0.1:<port>/` -> `skipped cookie with bad tailmatch domain: 127.0.0.2`
  - `n4=v; Path=/; Secure` over plain HTTP -> `skipped cookie because not 'secure'`
  - `noequals` and `=emptyname` -> `invalid cookie, dropped` (one line each)
  - `t=a<TAB>b; Path=/` and `c=a<0x01>b; Path=/` -> `invalid octets in value, cookie dropped`
  - nothing printed: a name and value over 4096 characters together, a header value over 4998 characters, `__Secure-a=1; Path=/` and `__Host-b=1; Path=/` without `Secure`.
- Before pinning, measure what is not yet measured: `Domain=.other.test` (is the dot printed?), a control character in the name, and a tab that ends an attribute part.
- Suggested shape: have the parser give a refusal reason (for example an enum, or the line to print) alongside the `Cookie?`, and have `CookieStore.StoreFromResponse` report it. `ParseFromCookieFile` stays silent: loading `-b` printed nothing in BL-367's measurement.

## Acceptance criteria

- [ ] A test in `Curl.Cookies.UnitTests` per measured line above asserts `CookieStore.StoreFromResponse` reports exactly that text to a recording `ITransferEvents`.
- [ ] A test asserts nothing is reported for each silent refusal listed above.
- [ ] The measurements still to take (see Context) are recorded in this task's Notes with the command and stderr bytes, and pinned in a test.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
