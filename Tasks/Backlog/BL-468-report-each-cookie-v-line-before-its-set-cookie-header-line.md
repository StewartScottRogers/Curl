---
id: BL-468
title: Report each cookie -v line before its Set-Cookie header line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-468 — Report each cookie -v line before its Set-Cookie header line

## Goal

With cookies on, `HttpProtocolHandler` reports each `Added cookie ...` (and dropped or replaced cookie) `-v` line immediately before the `< Set-Cookie: ...` header line it came from, as curl 8.21.0 does, instead of after the whole head.

## Context

- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`: `-s -v -c - http://127.0.0.1:18621/a` against `HTTP/1.1 200 OK`, `Set-Cookie: a=1`, `Content-Length: 2` printed `< HTTP/1.1 200 OK`, `* Added cookie a="1" for domain 127.0.0.1, path /, expire 0`, `< Set-Cookie: a=1`, `< Content-Length: 2`, `< `.
- Today `HttpProtocolHandler.StoreCookies` hands every `Set-Cookie` value to `ICookieStore.StoreFromResponse` after `HttpResponseHeadReader.ReadAsync` has reported every header line; since BL-449 the final head's empty line is held until after it, so the cookie lines land after the last header line and before `< `.
- `ICookieStore.StoreFromResponse` takes the whole list at once; storing one header at a time, as each is accepted in `HttpResponseHeadReader`, may be the simplest route that keeps `Curl.Protocol.Abstractions.UnitLibrary` unchanged (call it once per header). Measure a head with two `Set-Cookie` lines before pinning.

## Acceptance criteria

- [ ] A test over a scripted connection with a scripted cookie store pins each cookie line before its own `Set-Cookie` header event, as measured.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-449 (2026-09-27).

## Log

- 2026-09-27: Created.
