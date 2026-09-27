---
id: BL-182
title: Send and store cookies through ICookieStore in the HTTP handler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-161]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-182 — Send and store cookies through ICookieStore in the HTTP handler

## Goal

When an `ICookieStore` is supplied the handler sends its Cookie header and hands every Set-Cookie, including those on 3xx responses, back to the store.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H14. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ICookieStore.GetCookieHeader(Uri, bool secure, DateTimeOffset)` and `StoreFromResponse(Uri, IReadOnlyList<string>, DateTimeOffset)` (BL-161). Time comes from `ITransferContext.TimeProvider`.

## Acceptance criteria

- [x] The Cookie header from the store is sent in curl's position (measured); no header when the store returns null.
- [x] Every Set-Cookie value, including on a 3xx, is passed to `StoreFromResponse` in received order.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H14 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured on curl 8.21.0 (mingw64, Schannel) against a Python loopback listener, 2026-09-26. Jar file `jar.txt` holds `127.0.0.1 FALSE / FALSE 0 j k`.
  - `curl -b "a=b" -e ref -u u:p --compressed -H "X-A: 1" http://127.0.0.1:18082/` sends `Host`, `Authorization`, `User-Agent`, `Accept`, `Accept-Encoding`, `Referer`, `Cookie: a=b`, `X-A: 1`: Cookie goes after Referer and before the `-H` lines.
  - `curl -b jar.txt -H "Cookie: c=d"` sends `Cookie: j=k` then `Cookie: c=d`; `-H "Cookie:"` still sends `Cookie: j=k`. The cookie engine's line is never suppressed by `-H`.
  - `curl -b "a=b" -H "Cookie: c=d"` sends only `Cookie: c=d`; `-H "Cookie:"` sends no Cookie. A `-b name=value` string *is* suppressed. That lives in how `Curl.Console` feeds the store, so it is filed as BL-287.
  - `curl -b jar.txt -d x=1` puts `Cookie` before `Content-Length` and `Content-Type`.
  - `curl -b jar.txt -c - --anyauth -u u:p` against a 401 with `Set-Cookie: s=1`, `Set-Cookie: t=2` and a Basic challenge stores both and the retry sends `Cookie: t=2; s=1; j=k`: the store is asked afresh for every request, and a 401's cookies count.
- Implementation: `HttpRequestHeadFormatter.Format` takes a `cookie` value and writes it after `Referer`, never suppressed by `-H`; an empty value sends nothing. `HttpProtocolHandler` asks the store per request (secure = `https`, time from `ITransferContext.TimeProvider`) and hands the final head's `Set-Cookie` values, in received order, to `StoreFromResponse` right after the head is read, before it is written, so `-f` and write failures still store them. No call when the head has none. Set-Cookie on 1xx interim heads is not passed on (curl 8.21.0 was not measured for it; rare).
- No ADR: every rule here was measured, not chosen.
- Verified: `dotnet build -warnaserror` clean; fast tests green (Curl.Protocol.Http.UnitTests 532); `Measure-CodeQuality.ps1` reports Curl.Protocol.Http.UnitLibrary 100% line, 100% branch, 0 failing members. The run also hit the known flaky `AuthenticateAsClientAsync_WithoutClientCertificate_PresentsNone` in Curl.Networking once (BL-254); it passed on rerun.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. The HTTP handler sends ICookieStore's Cookie header after Referer and stores every Set-Cookie, 3xx and 401 included, in received order
