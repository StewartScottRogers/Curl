---
id: BL-1127
title: Print curl's cookie-limit -v lines through the HTTP handler
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1108]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
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

- [x] `ICookieStore.GetCookieHeader` takes the transfer's `ITransferEvents`; `HttpProtocolHandler` passes `context.Events`; `CookieEngine`'s `GroupCookies` passes them to the `CookieStore` overload, and `CookieStringSender` ignores them.
- [x] A test in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Cookies.cs` pins that the handler hands its events to the store and that a line the store reports comes before the request's header event.
- [x] A test in `Curl.Console.UnitTests` runs a request whose `-b` cookie file holds 151 cookies for the host and pins `* Included max number of cookies (150) in request!` in the `-v` output, through the in-process fakes, with no `TestCategory=Integration`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Protocol.Http.UnitLibrary` and `Curl.Console`.

## Notes

- 2026-10-01: Touches widened to `Curl.Cookies.UnitLibrary` and `Curl.Cookies.UnitTests`: `CookieStore` implements `ICookieStore`, so changing the contract needs a matching member there. No task in Doing on `origin/work/dark-factory` named either project.
- `ICookieStore.GetCookieHeader` now takes `ITransferEvents` (the model is `StoreFromResponse`). `CookieStore` gained `GetCookieHeader(url, secure, now, events)`, which uses the strings given to `AddCookieString`; the 3-argument overload stays for callers with no listener. The `IReadOnlyList<string>` null-argument test now casts its `null` because the two 4-argument overloads made it ambiguous.
- Tests: `HttpProtocolHandlerTests.ExecuteAsync_CookieStoreReportsALine_ReportsItBeforeTheRequestHeader`, `CurlCommandRunnerCookieTests.RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` (151 cookies in a `-b` file, 150 sent), and two `CookieStoreTests` for the new overload. Measure-CodeQuality: 0 failing members in Abstractions, Http, Console and Cookies.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. curl -v prints the cookie-limit lines through the HTTP handler before the request headers
