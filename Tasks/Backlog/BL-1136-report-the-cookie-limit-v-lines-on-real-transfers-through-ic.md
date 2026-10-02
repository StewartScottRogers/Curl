---
id: BL-1136
title: Report the cookie-limit -v lines on real transfers through ICookieStore
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1108]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1136 — Report the cookie-limit -v lines on real transfers through ICookieStore

## Goal

`curl -v` against a request whose stored cookies hit a limit prints curl 8.21.0's `* Included max number of cookies (150) in request!` and `* Restricted outgoing cookies due to header size, '<name>' not sent` lines, by passing the transfer's `ITransferEvents` to `CookieStore.GetCookieHeader`.

## Context

- BL-1108 added `CookieStore.GetCookieHeader(CurlUrl, bool, DateTimeOffset, IReadOnlyList<string>, ITransferEvents)` in `Curl.Cookies.UnitLibrary/CookieStore.cs`, which reports both lines; nothing calls it yet.
- `ICookieStore` (`Curl.Protocol.Abstractions.UnitLibrary`) has no events parameter on its header builder, so `Curl.Protocol.Http.UnitLibrary`'s `HttpProtocolHandler` and `Curl.Console/CookieEngine.cs` cannot pass the transfer's events. Add the events to the interface member (or a new one), and pass the transfer's `ITransferEvents` where the `Cookie` header is built.
- curl 8.21.0 sources: `lib/cookie.c` `Curl_cookie_getlist` (line 1351) and `lib/http.c` (line 2569), at https://github.com/curl/curl/tree/curl-8_21_0.
- Measure real curl with `Record-CurlExchange.ps1` (151 cookies in a `-b` file; cookies totalling more than 8183 characters) before pinning the stderr bytes.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` (or `Curl.Console.UnitTests`) shows a transfer with 150 matching stored cookies reporting `Included max number of cookies (150) in request!` as a `-v` info line, in the position real curl prints it relative to the request headers (measured with `Record-CurlExchange.ps1`, recorded under Notes).
- [ ] A test shows a transfer whose cookies exceed `CookieStore.LongestCookieHeader` reporting `Restricted outgoing cookies due to header size, '<name>' not sent`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every touched library.

## Notes

## Log

- 2026-10-01: Created.
