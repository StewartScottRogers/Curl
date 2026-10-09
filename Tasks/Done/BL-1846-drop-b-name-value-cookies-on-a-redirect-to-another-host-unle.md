---
id: BL-1846
title: Drop -b name=value cookies on a redirect to another host unless --location-trusted (upstream test2015)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1846 — Drop -b name=value cookies on a redirect to another host unless --location-trusted (upstream test2015)

## Goal

A `-b name=value` cookie string (curl's `CURLOPT_COOKIE`) is sent only to the first URL's host, not after `-L` follows a redirect to another host, unless `--location-trusted` is given, so upstream test2015 passes and GF-0010's item `behaviour:test2015` measures `match`.

## Context

- Split from BL-1803 (GF-0010). BL-1803 closed the trailing-dot Public Suffix List half (test1629) inside `Curl.Cookies.UnitLibrary`; this half cannot be done there.
- Evidence (GF-0010): test2015 runs `-b 'test=yes' -x <proxy> --location` to a new host; curl 8.21.0 sends no `Cookie:` on the second request, Curl sends `Cookie: test=yes`.
- Why not a cookie-store change: `CookieStore` cannot tell a redirect hop from a second URL on the command line, and curl sends the string to every URL of the group (BL-509). The decision belongs where `-H Cookie:` is already dropped: `Curl.Core.UnitLibrary/RedirectFollower.cs` (`HopHttp`, `sendCredentials = policy.LocationTrusted || IsSameOrigin(...)`). Carry a "send cookie strings" flag on the hop's `HttpRequestOptions` (or equivalent) and have `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `CookieHeaderFor` and `Curl.Console/CookieEngine.cs` (`GroupCookies`, `CookieStringSender`) leave the strings out when it is off. Stored cookies still follow their own domain rules. If `HttpRequestOptions` lives in `Curl.Protocol.Abstractions.UnitLibrary`, add it to `touches`.
- Measure real curl with `Record-CurlExchange.ps1` first: whether curl compares host only, or host, port and scheme (`Curl_auth_allowed_to_host`), and pin that.

## Acceptance criteria

- [x] A redirect from host A to host B with `-b test=yes -L` sends no `Cookie:` on B's request; a unit test pins it.
- [x] With `--location-trusted` the string is still sent to B; a same-host redirect still sends it; two command-line URLs on different hosts each still get it.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] No option is added or changed, so `--ai-help` stays as it is.

## Notes

- Added `Curl.Protocol.Abstractions.UnitLibrary` and its `.UnitTests` to `touches`: `HttpRequestOptions` and `ICookieStore` live there, and no task in Doing on `origin/work/dark-factory` names them (only BL-1828, `Curl.Cli`).
- Measurement: `Record-CurlExchange.ps1` hung on the 127.0.0.1 -> localhost redirect, so the rule is libcurl's `Curl_auth_allowed_to_host` (scheme, host and port) plus GF-0010's curl 8.21.0 measurement of test2015; it is the same test `RedirectFollower` uses for `-u` and `-H Cookie:` (ADR-0444).
- Design: `HttpRequestOptions.SendsCookieStrings` (default on), turned off by `RedirectFollower.HopHttp` when credentials are dropped; `ICookieStore.GetStoredCookieHeader` (default: `GetCookieHeader`) gives the stored cookies alone; `CookieEngine`'s `GroupCookies` and `CookieStringSender` implement it. Two command-line URLs are not follows, so each still gets the string.
- Tests: `RedirectFollowerTests.FollowAsync_Hop_SendsCookieStringsOnlyToTheFirstOriginOrWhenLocationTrusted`, `HttpProtocolHandlerTests.ExecuteAsync_SendsCookieStringsOff_AsksForTheStoredCookiesAlone`, `CookieEngineTests.GetStoredCookieHeader_*`, `ICookieStoreTests`. Build clean, fast tests green.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. -b name=value strings are no longer sent after -L follows a redirect to another scheme, host or port unless --location-trusted (ADR-0444)
