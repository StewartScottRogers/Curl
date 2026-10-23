---
id: BL-3177
title: Treat -b with an empty argument as cookie engine on with no open-failure warning
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary/CookieStore.cs, Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Cookies.UnitTests/CookieStoreEmptyCookieFileTests.cs, Curl.Console.UnitTests/CurlCommandRunnerEmptyCookieFileTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3177 — Treat -b with an empty argument as cookie engine on with no open-failure warning

## Goal

`curl -v -b '' http://host/` prints no cookie-file warning and turns the cookie engine on, as upstream does.

## Context

Stewart reported on 2026-10-22: `curl -v -b '' http://host/` prints `* WARNING: failed to open cookie file ""` from Curl; upstream is silent, since an empty `-b` argument just turns the cookie engine on (`CURLOPT_COOKIEFILE ""`). The warning text comes from `Curl.Cookies.UnitLibrary/CookieStore.cs` (`DescribeCookieFileOpenFailure`, reported near line 332 when the cookie file cannot be opened); the empty path must be treated as "engine on, nothing to read". Measure curl 8.22.0 with `Record-CurlExchange.ps1` for `-b ''`, `-b ''` followed by a `Set-Cookie` response then a redirect (cookies are kept and replayed in the same transfer), `-c -` combined with `-b ''`, and a nonexistent file name (which must still warn) and pin each. Check also how the engine decides the cookie engine is on for an empty value (`Curl.Engine.UnitLibrary/CookieEngine.cs`); change it only if the engine stays off.

## Acceptance criteria

- [x] `Curl.Cookies.UnitTests/CookieStoreEmptyCookieFileTests.cs` shows an empty path loads nothing, reports no event, and leaves the store enabled.
- [x] A test in `Curl.Console.UnitTests/CurlCommandRunnerEmptyCookieFileTests.cs` runs `-v -b ''` against a fake HTTP server and expects no `WARNING: failed to open cookie file` line, and expects a cookie set by the first response to be sent on a redirect hop (engine on).
- [x] A nonexistent `-b` path still prints the warning exactly as before (existing tests unchanged).
- [x] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Cookies.UnitTests` and `Curl.Console.UnitTests` pass; the tests use only drive-less, platform-neutral paths.

## Notes

- Measured curl 8.22.0 (Git mingw, Schannel) on 2026-10-22 with Record-CurlExchange.ps1: `-v -b ''` writes no cookie warning, exit 0; `-v -L -b '' -c -` against a 302 with `Set-Cookie: a=b` sends `Cookie: a=b` on the redirect hop and writes the cookie to the jar; `-v -b nosuchfile.txt` still writes `* WARNING: failed to open cookie file "nosuchfile.txt"`.
- Fix: `CookieStore.LoadCookieFileAsync` returns at once for an empty path, as `lib/cookie.c` opens nothing for `*file == 0`. The engine already turned on for `-b ''` (`CookieEngine` counts `CookieFiles`, and the existing `RunAsync_FollowedRedirectWithEmptyCookieFileName_SendsTheCookieSetByTheFirstHop` passed), so `CookieEngine.cs` is unchanged. No ADR: this only matches measured curl, no design choice.
- Existing nonexistent-path tests (`LoadCookieFileAsync_CannotOpen_ReportsTheWarning`, `RunAsync_VerboseCookieFileThatCannotBeOpened_PrintsCurlsWarningFirst`) unchanged and green. New tests use no file-system paths. Fast tests: Curl.Cookies.UnitTests 563 passed, Curl.Console.UnitTests 2832 passed. Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: abb48cca 27151861 7e71333d c06e9478.
- 2026-10-22: Doing -> Done. -b '' turns the cookie engine on silently, as curl 8.22.0 does
