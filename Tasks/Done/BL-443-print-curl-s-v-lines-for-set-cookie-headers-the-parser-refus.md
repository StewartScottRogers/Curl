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
completed: 2026-09-27
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

- [x] A test in `Curl.Cookies.UnitTests` per measured line above asserts `CookieStore.StoreFromResponse` reports exactly that text to a recording `ITransferEvents`.
- [x] A test asserts nothing is reported for each silent refusal listed above.
- [x] The measurements still to take (see Context) are recorded in this task's Notes with the command and stderr bytes, and pinned in a test.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Measured 2026-09-27, curl 8.21.0 (mingw64, Schannel): `Record-CurlExchange.ps1 -Response "HTTP/1.1 200 OK\r\nSet-Cookie: <header>\r\nContent-Length: 0\r\n\r\n" -CurlArgs -s,-v,--resolve,www.example.co.uk:<port>:127.0.0.1,-c,-,http://www.example.co.uk:<port>/`. stderr line after `* ` (bytes checked with `od -c` where they matter):
  - The three the task asked for: `Domain=.other.test` -> `skipped cookie with bad tailmatch domain: other.test` (dot not printed); `n\x01a=v` -> `invalid octets in name, cookie dropped`; `Secure<TAB>; Path=/` -> `skipped cookie because not 'secure'` (a tab ending an attribute's name still applies it), `Pa<TAB>th=/x; Domain=other.test` -> `Added cookie n="v" ... path /` (reading ends at the tab; the rest is ignored).
  - The tailmatch line prints the **rest of the header** from the failing Domain value (curl's `%s` of a pointer into the line): `Domain=other.test ; Path=/` -> `other.test ; Path=/`; `Domain=  .other.test;Path=/` -> `other.test;Path=/`; `Domain=other.test   ` -> `other.test   ` (trailing blanks kept); `Domain=other.test<TAB>; Path=/` -> `other.test\t; Path=/`; `Domain=".other.test"` -> `".other.test"`; `Domain=OTHER.Test` -> `OTHER.Test`; `Domain=www.example.co.uk; Domain=x.test; Path=/` -> `x.test; Path=/`; `Domain=other.test; X=a\x01` -> `other.test; X=a\x01` (the 0x01 byte printed).
  - Control characters are checked per part, in order, not over the whole header: `Secure; Path=/a\x01b` -> not 'secure'; `Path=/a\x01b; Secure` -> invalid octets in value; `Pa<TAB>th=/; X=\x01` -> Added (never checked). Name octets before value octets (`\x01n=a\x01`, `n\x01=a<TAB>b` -> name), octets before `invalid cookie` (`n\x01` -> name, `=a\x01` -> value), and `invalid cookie` before the silent length limit (`=`+4200 chars, 4200 chars without `=` -> invalid cookie; 4000`=`200 + `\x01` -> invalid octets in value). Also: `Pa\x01th=/`, `Foo\x02=bar`, `Sec\x01ure` -> name; `n=a\x7Fb`, `=\x01` attribute -> value; ` =v` and `n<TAB>b=v` -> invalid cookie.
  - `-b` file with `Set-Cookie: f=v; Pa<TAB>th=/; X=\x01` and `Set-Cookie: g=v; X=\x01` sent `Cookie: f=v` and printed `* invalid octets in value, cookie dropped`: the per-part rule holds for file lines too (pinned in `SetCookieParserTests`), and curl does print refusals while loading a file, which `ParseFromCookieFile` still leaves silent. Filed as BL-460.
- Shape: `SetCookieParser.Parse(headerValue, requestUrl, now, out string? refusal)` gives the line, or null where curl prints nothing; the three-argument overload delegates to it. `CookieStore.StoreFromResponse` reports it. A refused header is reported but does not count towards the 50-per-response limit.
- Behaviour change beyond reporting: the parser no longer refuses a control character anywhere in the header up front; it checks each part's name and value as curl does, so a control character after a tab-ended part is now accepted, matching the measurement.
- No ADR: no choice was made that curl's measured behaviour did not dictate.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v prints curl 8.21.0's line for each Set-Cookie header the parser refuses, and nothing where curl is silent
