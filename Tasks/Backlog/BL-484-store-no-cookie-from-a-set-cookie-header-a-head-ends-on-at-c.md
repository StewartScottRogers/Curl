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
completed:
---
# BL-484 — Store no cookie from a Set-Cookie header a head ends on at close, as curl 8.21.0 does

## Goal

When the peer closes the connection right after a final head's `Set-Cookie` line, with no byte of a next line, `HttpProtocolHandler` stores no cookie from it, as curl 8.21.0 does, while still reporting the line in `-v` and writing it for `-D`.

## Context

- curl 8.21.0 acts on a header only once a byte of the next line shows it whole, so it never acts on the header a head ends on at close (BL-483 Notes). BL-483 applied that to framing, persistence and redirects through `HttpResponseHeadReader.HeadActedOn`.
- `HttpResponseHeadReader` still tells `HeaderReceived` (which stores cookies) of that last header: `ReadHeaderLinesAsync` releases it at close and `ReleaseHeldHeaders` tells it.
- Measured (2026-09-27, `Record-CurlExchange.ps1`, `-s -c - http://127.0.0.1:<port>/`) against `HTTP/1.1 200 OK\r\nX-Before: 1\r\nSet-Cookie: a=1\r\n` then close: exit 0, the cookie jar holds only its three header lines. Measure `-v` too (curl may print no `Added cookie` line) before pinning.

## Acceptance criteria

- [ ] A test over a scripted connection that closes right after `Set-Cookie: a=1\r\n` pins that the cookie store is not asked, and the measured `-v` lines.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-483 (2026-09-27).

## Log

- 2026-09-27: Created.
