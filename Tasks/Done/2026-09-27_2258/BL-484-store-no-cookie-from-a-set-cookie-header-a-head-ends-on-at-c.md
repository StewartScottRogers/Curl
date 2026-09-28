---
id: BL-484
title: Store no cookie from a Set-Cookie header a head ends on at close, as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-484 — Store no cookie from a Set-Cookie header a head ends on at close, as curl 8.21.0 does

## Goal

When the peer closes the connection right after a final head's `Set-Cookie` line, with no byte of a next line, `HttpProtocolHandler` stores no cookie from it, as curl 8.21.0 does, while still reporting the line in `-v` and writing it for `-D`.

## Context

- curl 8.21.0 acts on a header only once a byte of the next line shows it whole, so it never acts on the header a head ends on at close (BL-483 Notes). BL-483 applied that to framing, persistence and redirects through `HttpResponseHeadReader.HeadActedOn`.
- `HttpResponseHeadReader` still tells `HeaderReceived` (which stores cookies) of that last header: `ReadHeaderLinesAsync` releases it at close and `ReleaseHeldHeaders` tells it.
- Measured (2026-09-27, `Record-CurlExchange.ps1`, `-s -c - http://127.0.0.1:<port>/`) against `HTTP/1.1 200 OK\r\nX-Before: 1\r\nSet-Cookie: a=1\r\n` then close: exit 0, the cookie jar holds only its three header lines. Measure `-v` too (curl may print no `Added cookie` line) before pinning.

## Acceptance criteria

- [x] A test over a scripted connection that closes right after `Set-Cookie: a=1\r\n` pins that the cookie store is not asked, and the measured `-v` lines.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-483 (2026-09-27).
- Measured 2026-09-27 with `Record-CurlExchange.ps1 -CurlArgs -v,-c,-` against `HTTP/1.1 200 OK\r\nX-Before: 1\r\nSet-Cookie: a=1\r\n` then close: exit 0; `-v` prints `< HTTP/1.1 200 OK`, `< X-Before: 1`, `< Set-Cookie: a=1` with no `* Added cookie` line and no empty line, then `* Connection #0 ... left intact`; the jar holds only its three header lines. Control, the same head plus one byte `X` of a next line: `* Added cookie a="1" for domain 127.0.0.1, path /, expire 0` before `< Set-Cookie: a=1`, and the jar holds the cookie.
- Plan (small, done in-session rather than through separate architect/implementer agents): `HttpResponseHeadReader.ReleaseHeadBeforeRefusal` marks the last held header of a final head the peer closed among its headers, with none refused, as not acted on (`HeldHeader.IsActedOn = false`), the same rule as `HeadActedOn`; `ReleaseHeldHeaders` still reports its lines but does not tell `HeaderReceived`, which is what stores cookies. No handler change was needed. Test: `HttpProtocolHandlerTests.ExecuteAsync_PeerClosesRightAfterASetCookie_StoresNoCookieFromIt` (earlier `Set-Cookie: b=2` stored, `a=1` not asked, head lines and `-D` bytes as received, connection left intact), at every chunk size.
- No ADR: this applies BL-483's measured rule, not a new decision.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A Set-Cookie header a head ends on at close is reported and written but stores no cookie, as curl 8.21.0 does
