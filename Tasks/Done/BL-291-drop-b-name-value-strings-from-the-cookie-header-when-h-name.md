---
id: BL-291
title: Drop -b name=value strings from the Cookie header when -H names Cookie
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-182]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-291 — Drop -b name=value strings from the Cookie header when -H names Cookie

## Goal

`curl -b "a=b" -H "Cookie: c=d" URL` sends only `Cookie: c=d`, as curl 8.21.0 does, while cookies from a `-b` file or received `Set-Cookie` are still sent beside it.

## Context

- Measured on curl 8.21.0 (BL-182 Notes): a `-b name=value` string (`CURLOPT_COOKIE`) is left out when an `-H` value names `Cookie` (`-H "Cookie:"` included), but the cookie engine's cookies (from a `-b` file or `Set-Cookie`) are sent on their own `Cookie` line after `Referer`, before the `-H` lines, whatever `-H` names.
- `CookieStore.GetCookieHeader` (Curl.Cookies.UnitLibrary) appends the strings given to `AddCookieString` to the value it returns, and `HttpProtocolHandler` (BL-182) sends that value whatever `-H` names. So once `Curl.Console` wires `-b` strings into the store, `-b a=b -H "Cookie: c=d"` would send `Cookie: a=b` and `Cookie: c=d`.
- Where to start: wherever `Curl.Console` builds the `CookieStore` from `-b` (BL-221 added `AddCookieString`); skip `AddCookieString` when a `-H` value names `Cookie` (case-insensitive, `Cookie:` and `Cookie;` forms included).

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test shows `-b "a=b" -H "Cookie: c=d"` gives a store whose `GetCookieHeader` returns null for the URL, and `-b "a=b"` alone gives `a=b`.
- [x] A test shows `-b jar.txt -b "a=b" -H "Cookie: c=d"` keeps the jar's cookies in the store's value and drops `a=b`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

- Filed from BL-182 (2026-09-26). If `Curl.Console` does not yet build a `CookieStore` from `-b`, add the task that wires it to `depends-on`.
- 2026-09-27: `CookieEngine` already builds the store from `-b` (BL-237), so no dependency was needed. Its constructor now skips `AddCookieString` when any `-H` value starts with `Cookie:` or `Cookie;` (ordinal, case-insensitive), which is how curl's `Curl_checkheaders` matches a name followed by `Curl_headersep`. `-b` files still load. No new output text, so nothing was re-measured; the behaviour is the one BL-182 measured. Tests: `CookieEngineTests` (8 cases, including `Cookies:` and `X-Cookie:`, which do not match).
- 2026-09-27: `Measure-CodeQuality.ps1` flags `DiskWriteOutFileOpener.TryOpen` in the fast run only, because its tests are `Integration` by design (BL-280). With `-IncludeIntegration`, `Curl.Console` is 100/100 with no failing member. Splitting the `-b` loop into `AddCookies` keeps the constructor under complexity 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -b name=value strings are left out of the Cookie header when an -H value names Cookie; -b file cookies still sent
