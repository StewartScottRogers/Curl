---
id: BL-1391
title: Join -b cookie strings as curl's tool does and refuse a joined string of 8200 bytes or more with exit 100
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1390]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-098
created: 2026-10-03
completed:
---
# BL-1391 — Join -b cookie strings as curl's tool does and refuse a joined string of 8200 bytes or more with exit 100

## Goal

Several `-b name=value` strings are joined into one `Cookie` value exactly as curl 8.21.0's tool joins them - `;` then a space unless the next string already starts with a blank - and a joined string of 8200 bytes or more fails the transfer before it connects with exit 100, after `Warning: skipped provided cookie, the cookie header would go over 8200 bytes` (unless `-s`).

## Context

- Today `Curl.Console/CookieEngine.cs` hands the group's `-b` strings on as a list: `CookieStringSender.GetCookieHeader` joins them with `string.Join("; ", cookieStrings)` and `GroupCookies` passes them to `CookieStore.GetCookieHeader` (`Curl.Cookies.UnitLibrary/CookieStore.cs` line 183, `AppendJoin("; ", cookieStrings)`), so `-b a=1 -b " b=2"` sends `a=1;  b=2` (two spaces) where curl sends `a=1; b=2`, and there is no length limit (`git grep 8200` finds nothing in `Curl.Console` or `Curl.Cli.UnitLibrary`).
- curl 8.21.0 (tag `curl-8_21_0`), `src/config2setopts.c` lines 515-542 (`cookie_setopts`): the strings are joined into one `dynbuf` with a maximum of `MAX_COOKIE_LINE` 8200 - the first as is, each later one as `";%s%s"` with `" "` only when its first character is not a blank (`ISBLANK`: space or tab) - and when the buffer would reach the maximum, `warnf("skipped provided cookie, the cookie header would go over %d bytes", MAX_COOKIE_LINE)` and the add's `CURLE_TOO_LARGE` (exit 100, `A value or data field grew larger than allowed`) ends the transfer. The one joined string is then given to libcurl as `CURLOPT_COOKIE`, which sends it after any stored cookies.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`:
  - `-sv -b a=1 -b " b=2" -b "<TAB>c=3"`: `> Cookie: a=1; b=2;<TAB>c=3`; the same with `-c NUL` (cookie engine on): `> Cookie: a=1; b=2`.
  - one `-b` string of 8199 bytes: sent, exit 0; of 8200 bytes: nothing sent, exit 100, `curl: (100) A value or data field grew larger than allowed` under `-sS`.
  - `-v -o NUL -b <5002 bytes> -b <4002 bytes>`: first stderr line `Warning: skipped provided cookie, the cookie header would go over 8200 bytes`, then `curl: (100) A value or data field grew larger than allowed`, no connection; under `-s` the warning is not written.
  - two strings of 4098 and 4097 bytes (8197 joined with `; `): sent, exit 0.

## Acceptance criteria

- [ ] Tests in `Curl.Console.UnitTests` pin the sent `Cookie` header for `-b a=1 -b " b=2" -b "\tc=3"` with the cookie engine off and for `-b a=1 -b " b=2" -c <jar>` with it on, as measured.
- [ ] Tests pin that one string of 8199 bytes is sent, and that 8200 bytes - as one string, and as two strings whose joined length is 8200 - give exit 100 with `curl: (100) A value or data field grew larger than allowed`, the wrapped warning line first unless `-s`, and no connection made (the fake connector records none).
- [ ] `CookieStore` (in `Curl.Cookies.UnitLibrary`) is not changed: Curl.Console hands it the one joined string.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member in the code this task changed.

## Notes

- Depends on BL-1390 only because both change `Curl.Console`; no option changes, so `--ai-help` is unaffected.
- Exit 100 is `CurlExitCode.TooLarge` (already in `Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`); this task does not change Abstractions.

## Log

- 2026-10-03: Created.
