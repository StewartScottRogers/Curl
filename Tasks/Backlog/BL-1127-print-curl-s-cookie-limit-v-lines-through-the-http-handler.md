---
id: BL-1127
title: Print curl's cookie-limit -v lines through the HTTP handler
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1108]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1127 — Print curl's cookie-limit -v lines through the HTTP handler

## Goal

`curl -v` prints `* Included max number of cookies (150) in request!` and `* Restricted outgoing cookies due to header size, '<name>' not sent` when a request's `Cookie` header hits curl's limits, by passing the transfer's events to the events-taking `CookieStore.GetCookieHeader` overload BL-1108 adds.

## Context

- BL-1108 makes `Curl.Cookies.UnitLibrary/CookieStore.cs` report both lines to an `ITransferEvents` through a new `GetCookieHeader` overload; read its Notes for the overload's final shape. The texts and when curl prints them are in BL-1108 (curl 8.21.0 `lib/cookie.c` line 1351, `lib/http.c` line 2569).
- Today the events cannot reach it: `Curl.Protocol.Abstractions.UnitLibrary/ICookieStore.cs` `GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now)` has no events parameter (`StoreFromResponse` already takes one, which is the model to follow); `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `CookieHeaderFor` calls it without `context.Events`; `Curl.Console/CookieEngine.cs` implements it twice (`GroupCookies`, `CookieStringSender`), and `Curl.Protocol.Http.UnitTests/Fakes/ScriptedCookieStore.cs` fakes it. `Curl.Console/CurlComposition.cs` also names `ICookieStore`.
- curl prints the lines while it builds the request, so they come before the request's `>` header lines in `-v` output.
- No command-line option changes, so `--ai-help` is untouched.

## Acceptance criteria

- [ ] `ICookieStore.GetCookieHeader` takes the transfer's `ITransferEvents`; `HttpProtocolHandler` passes `context.Events`; `CookieEngine`'s `GroupCookies` passes them to the `CookieStore` overload, and `CookieStringSender` ignores them.
- [ ] A test in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Cookies.cs` pins that the handler hands its events to the store and that a line the store reports comes before the request's header event.
- [ ] A test in `Curl.Console.UnitTests` runs a request whose `-b` cookie file holds 151 cookies for the host and pins `* Included max number of cookies (150) in request!` in the `-v` output, through the in-process fakes, with no `TestCategory=Integration`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Protocol.Http.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-10-01: Created.
