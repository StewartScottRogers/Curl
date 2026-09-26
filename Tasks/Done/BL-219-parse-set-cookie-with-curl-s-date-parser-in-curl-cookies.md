---
id: BL-219
title: Parse Set-Cookie with curl's date parser in Curl.Cookies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-151]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-219 — Parse Set-Cookie with curl's date parser in Curl.Cookies

## Goal

`Curl.Cookies.UnitLibrary` parses Set-Cookie values into cookies with curl's attribute handling and date parser.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Attributes: Expires, Max-Age, Domain, Path, Secure, HttpOnly, `__Secure-`/`__Host-` prefixes.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each attribute and prefix rule is measured on curl 8.21.0 (via `-c` jar output) and pinned.
- [x] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Plan item: Q1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-26: Plan (in-session, no separate architect run: one library, no new seam). `SetCookieParser.Parse(string headerValue, Uri requestUri, DateTimeOffset now)` returns a `Cookie` record or `null` where curl drops the cookie; `CookieDateParser.TryParse(string, out long unixSeconds)` ports libcurl's `parsedate`. The inputs follow the ADR-0014 `ICookieStore.StoreFromResponse` contract (a `Uri`, verbatim header values, a passed `now`), so BL-220 can call the parser directly.
- Measured on curl 8.21.0 (`/mingw64/bin/curl`, Schannel, libpsl) on 2026-09-26, about 120 cases. Each was served by `Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK
Set-Cookie: <header>
Content-Length: 0

' -CurlArgs -s,--resolve,<host>:<port>:127.0.0.1,-c,jar.txt,http://<host>:<port><path>`, and the jar line curl wrote is pinned field for field in `SetCookieParserTests` (one `DataRow` per case, the header and the jar line as measured). Examples: `a=1` at `/a/b/c?q=1` gave `example.com FALSE /a/b FALSE 0 a 1`; `l=12; HttpOnly` gave `#HttpOnly_example.com FALSE / FALSE 0 l 12`; `g=7; Domain=example.com` gave `.example.com TRUE / FALSE 0 g 7`; `n=14; Expires=Wed, 09 Jun 2100 10:18:14 GMT` at now 1790458978 gave expiry 1825018980 (the 400-day cap, `(now + 34560000 + 30) / 60 * 60`).
- What was learned from the measurements (all pinned):
  - Secure origin: `Secure` from `http://example.com` or `http://foo.localhost` drops the cookie; from `localhost` and `127.0.0.1` it is kept. `https`, `wss` and `::1` follow libcurl's `Curl_secure_context` source; they were not measured, because the recorder has no TLS listener and listens on IPv4 only.
  - `Secure`/`HttpOnly` count only without `=` (`Secure=`, `secure=yes`, `HTTPONLY=1` are ignored). `Path`/`Domain`/`Max-Age`/`Expires` need `=` and a value that is not empty.
  - Name ends at `=` or a tab; the first part without `=`, or with an empty name, drops the cookie. A part that ends at a tab ends parsing (`u=v;	Secure` is not secure). A tab inside a trimmed value, and any control character other than tab anywhere, drops the cookie.
  - Limits: header value (after the colon, leading space counted) at most 4998 characters; name plus value at most 4096; attributes are not limited individually.
  - `Max-Age` always wins over `Expires`, and the last one wins; `Expires` counts only when no expiry was read yet and it is under 80 characters; a date curl cannot read leaves a session cookie; a date at or before the epoch, `Max-Age=0`, `-1`, `abc` and `+100` all mean already expired (expiry 1).
  - `__Secure-`/`__Host-` prefixes are case-sensitive (`__secure-ab=28` is kept without `Secure`).
- Choice: `CookieDateParser` is a second copy of `Curl.Cli`'s `CurlDateParser`, returning Unix seconds with curl's full range and its year-1583 floor. `Curl.Cookies` cannot reference `Curl.Cli`, and moving the parser is outside this task's `touches`. Filed BL-251 to share one port and to give `-z` the 1583 floor it lacks.
- Choice: an already expired cookie is returned with expiry 1 rather than refused. curl keeps it this way so that the store deletes the stored cookie of the same name (BL-220).
- Choice: the Public Suffix List is not consulted here (`Domain=com` from `example.com` is dropped by curl through libpsl); that belongs to BL-223.
- Choice: the header-length limit counts from right after the colon. That is how curl counted it in the measurement (a leading space counts). If the HTTP layer strips that space before calling the store, it must pass the raw value.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SetCookieParser reads Set-Cookie into Cookie with curl 8.21.0's attribute, prefix, limit and Expires/Max-Age rules, measured and pinned
