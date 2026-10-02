---
id: BL-1108
title: Report curl's cookie-limit -v lines when the Cookie header is built
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1108 — Report curl's cookie-limit -v lines when the Cookie header is built

## Goal

`CookieStore` can build the `Cookie` header while reporting, to an `ITransferEvents`, the two `-v` lines curl 8.21.0 prints when a limit cuts the header short: `Included max number of cookies (150) in request!` and `Restricted outgoing cookies due to header size, '<name>' not sent`.

## Context

- curl 8.21.0, `lib/cookie.c` `Curl_cookie_getlist` (line 1351 at https://github.com/curl/curl/blob/curl-8_21_0/lib/cookie.c): each matching cookie is appended and counted, and `if(matches >= MAX_COOKIE_SEND_AMOUNT) { infof(data, "Included max number of cookies (%zu) in request!", matches); break; }` - so the line appears as soon as the 150th match is taken, also when exactly 150 match.
- curl 8.21.0, `lib/http.c` line 2569 (https://github.com/curl/curl/blob/curl-8_21_0/lib/http.c): while building the header, `if(clen + add >= MAX_COOKIE_HEADER_LEN) { infof(data, "Restricted outgoing cookies due to header size, '%s' not sent", co->name); linecap = TRUE; break; }`, and with `linecap` the `-b name=value` strings are not added either.
- Curl today: `Curl.Cookies.UnitLibrary/CookieStore.cs` `GetCookieHeader(CurlUrl, bool, DateTimeOffset, IReadOnlyList<string>)` already applies both limits (`MostCookiesSent` = 150 through `Take`, `LongestCookieHeader` = 8183 through `TryAppendCookies`) but has no events parameter, so neither line can be reported. `CookieStore` already reports other `-v` lines through `ITransferEvents` (`StoreReceived`, `LoadCookieFileAsync`), and `Curl.Cookies.UnitTests/CookieStoreTests.Verbose.cs` tests them with `Fakes/RecordingTransferEvents.cs`.
- Add an overload taking `ITransferEvents` (keep the existing overloads, which report to nothing, so `Curl.Console/CookieEngine.cs` and `Curl.Protocol.Http.UnitLibrary` still build untouched). Passing the events through `ICookieStore` and `HttpProtocolHandler` touches `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Protocol.Http.UnitLibrary` and `Curl.Console`, and is a follow-up task, not this one.

## Acceptance criteria

- [ ] New tests in `Curl.Cookies.UnitTests/CookieStoreTests.Verbose.cs`: 151 stored cookies matching one URL report exactly one `Included max number of cookies (150) in request!` and send 150; exactly 150 report it too; 149 report nothing.
- [ ] New tests: cookies whose values push the header past `LongestCookieHeader` report `Restricted outgoing cookies due to header size, '<name>' not sent` once, naming the first cookie left out, and the `-b` strings are not appended after it.
- [ ] The existing `GetCookieHeader` overloads return the same values as before (existing tests unchanged and passing).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
