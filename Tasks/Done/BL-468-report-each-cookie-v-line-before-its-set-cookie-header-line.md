---
id: BL-468
title: Report each cookie -v line before its Set-Cookie header line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Console]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-468 — Report each cookie -v line before its Set-Cookie header line

## Goal

With cookies on, `HttpProtocolHandler` reports each `Added cookie ...` (and dropped or replaced cookie) `-v` line immediately before the `< Set-Cookie: ...` header line it came from, as curl 8.21.0 does, instead of after the whole head.

## Context

- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`: `-s -v -c - http://127.0.0.1:18621/a` against `HTTP/1.1 200 OK`, `Set-Cookie: a=1`, `Content-Length: 2` printed `< HTTP/1.1 200 OK`, `* Added cookie a="1" for domain 127.0.0.1, path /, expire 0`, `< Set-Cookie: a=1`, `< Content-Length: 2`, `< `.
- Today `HttpProtocolHandler.StoreCookies` hands every `Set-Cookie` value to `ICookieStore.StoreFromResponse` after `HttpResponseHeadReader.ReadAsync` has reported every header line; since BL-449 the final head's empty line is held until after it, so the cookie lines land after the last header line and before `< `.
- `ICookieStore.StoreFromResponse` takes the whole list at once; storing one header at a time, as each is accepted in `HttpResponseHeadReader`, may be the simplest route that keeps `Curl.Protocol.Abstractions.UnitLibrary` unchanged (call it once per header). Measure a head with two `Set-Cookie` lines before pinning.

## Acceptance criteria

- [x] A test over a scripted connection with a scripted cookie store pins each cookie line before its own `Set-Cookie` header event, as measured.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-449 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -v -c - http://127.0.0.1:<port>/a`:
  - `Set-Cookie: a=1`, `X-Mid: y`, `Set-Cookie: b=2; Path=/`, `Set-Cookie: a=3`, `Content-Length: 2`: each `Added`/`Replaced cookie` line comes immediately before its own `< Set-Cookie:` line.
  - A 1xx head's `Set-Cookie: z=9` (`HTTP/1.1 100 Continue`) is stored and reported before its header line; the jar holds it.
  - A folded `Set-Cookie: a=1;` + ` Path=/x` is stored whole (path `/x`) and its line printed before the header (curl prints the header unfolded as one line; Curl still reports the two raw lines, unchanged here).
  - `Set-Cookie: a=1; Max-Age=0` then `Set-Cookie: a=2`: `a=2` is `Added`, not `Replaced` - the expired cookie is gone before the next header is stored.
- Plan and decisions (decided by Claude under Stewart's delegation; recorded here, not in an ADR, because `Documentation/Planning/Decisions` is in BL-466's `touches` and every choice follows a measurement):
  - `HttpResponseHeadReader` holds each header line until the next line shows the header is whole (folding), then tells `HeaderReceived` of the folded header and reports its lines. The handler stores each `Set-Cookie` there, in every head, 1xx included, as measured. `ReportHeldEmptyLine` became `ReportHeldLines`: a failure mid-head still reports the held lines, but hands no cookie to the store (as before, when a failed head stored nothing).
  - `ICookieStore.StoreFromResponse` now takes one header and a running count and returns the new count, so the store keeps curl's 50-cookies-per-response limit across per-header calls and stays stateless under `--parallel`. `CookieStore` keeps the list overload for its own tests as successive single-header calls. Removing expired cookies after each header matches the `Max-Age=0` measurement above.
  - `touches` widened to `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Cookies.UnitLibrary`, `Curl.Cookies.UnitTests` and `Curl.Console` for that contract change; no task in `Doing` named any of them (BL-466: Ftp + Decisions; BL-472: Console.UnitTests).
  - ADR-0014 still shows the original `StoreFromResponse(Uri, ...)` sketch; it is a historical record and in BL-466's `touches`, so left alone.
- Follow-up filed: BL-475 (a refused header's later headers are still reported, and now their cookies stored).
- Verified: `dotnet build -warnaserror` clean; fast tests all green (Http 1121, Cookies 302, Console 931); `Measure-CodeQuality.ps1`: Http, Cookies, Abstractions 100/100 with no failing member. Console's two failing members (`DiskWriteOutFileOpener.TryOpen`, `DumpHeaderOutputStream.WriteAsync`) predate this task and are BL-432, BL-455, BL-462.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Each cookie -v line is reported right before its Set-Cookie header line, 1xx heads and folded headers included
