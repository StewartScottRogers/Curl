---
id: BL-1225
title: Write curl's 'oversized cookie dropped' -v line for a Set-Cookie whose name and value pass 4096 bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1225 — Write curl's 'oversized cookie dropped' -v line for a Set-Cookie whose name and value pass 4096 bytes

## Goal

`SetCookieParser` refuses a cookie whose name and value together are longer than 4096 bytes with the `-v` line curl 8.21.0 writes, `oversized cookie dropped, name/val <name length> + <value length> bytes`, instead of refusing it silently.

## Context

- Today `Curl.Cookies.UnitLibrary/SetCookieParser.cs` refuses such a cookie (`LongestNameAndValue` = 4096, line 25) but its remarks (around line 104) say "curl prints nothing for ... a name and value longer than `LongestNameAndValue`", so the `refusal` out value is `null` for it. That is not what curl does.
- curl 8.21.0, `lib/cookie.c` `parse_first_pair` lines 437-441 at `curl-8_21_0`: `if((curlx_strlen(name) + curlx_strlen(val)) > MAX_NAME) { infof(data, "oversized cookie dropped, name/val %zu + %zu bytes", ...); return FALSE; }`, with the name and value as trimmed for the cookie.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port <p> -Response 'HTTP/1.1 200 OK\r\nSet-Cookie: <4000 a>=<97 b>\r\nContent-Length: 0\r\n\r\n' -CurlArgs '-v','-c','-','http://127.0.0.1:<p>/'` wrote `< HTTP/1.1 200 OK`, `* oversized cookie dropped, name/val 4000 + 97 bytes`, `< Set-Cookie: aaaa...`, `< Content-Length: 0`, and a cookie jar with no cookie; exit 0.
- Refused cookies already reach `-v` through the refusal string (`invalid cookie, dropped` and the others BL-1108 added); the HTTP library prints whatever the parser returns, so no other library changes.
- The 4998-byte header limit (`LongestHeaderValue`) and the `__Secure-`/`__Host-` prefixes are checked elsewhere; leave them as they are unless measuring shows curl reaches this check first.

## Acceptance criteria

- [x] Tests in `Curl.Cookies.UnitTests` pin: a 4000-byte name with a 97-byte value refuses the cookie with exactly `oversized cookie dropped, name/val 4000 + 97 bytes`; a name and value of exactly 4096 bytes together are stored; the counts are of the trimmed name and value (a value written with surrounding spaces is counted without them).
- [x] The remark that curl prints nothing for an oversized name and value is corrected to say what curl prints, citing the measurement.
- [x] Every other Cookies test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `TrySetNameAndValue` now refuses an oversized name and value with `oversized cookie dropped, name/val <n> + <v> bytes`, counting the trimmed name and value. The octet checks still run first, so the measured `LongNameAndValueWithAControlCharacter` case keeps `invalid octets in value`. Cookie-file lines get the same line, since curl shares `parse_first_pair`.
- One existing test changed: `CookieStoreTests.StoreFromResponse_NameAndValueTooLong_ReportsNothing` pinned the old silence that the task corrects. It is now `..._ReportsOversizedCookieDropped` and expects `name/val 4000 + 200 bytes`. Every other Cookies test passes unchanged.
- Results: Cookies 353 tests green; Measure-CodeQuality reports 100% line, 100% branch, 0 failing, worst CRAP 10; solution build clean with -warnaserror; fast tests green.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. SetCookieParser reports curl's 'oversized cookie dropped, name/val n + v bytes' -v line
