---
id: BL-1177
title: Write Ignoring duplicate digest auth header. for a proxy 407 when no proxy credentials were given
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1175]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1177 — Write Ignoring duplicate digest auth header. for a proxy 407 when no proxy credentials were given

## Goal

Under `-v`, a forward proxy's `407` whose `Proxy-Authenticate` headers carry more than one Digest challenge writes `* Ignoring duplicate digest auth header.` before each later one even when no proxy credentials (`-U`) were given, as curl 8.21.0 does for the origin's `401` without `-u` (BL-1175 Notes).

## Context

BL-1175 made `HttpAuthProblemLines` write the duplicate line, but `HttpProtocolHandler.ReportAuthProblemLines` reads the proxy's headers only when `plan.ProxyAuthRequest` is set, which it is not without proxy credentials. Measure first with `Record-CurlExchange.ps1`: `curl -s -S -v -x http://127.0.0.1:<P> http://example.invalid/` against a `407` with `Proxy-Authenticate: Digest realm="r", nonce="a", Digest realm="s", nonce="c"`.

## Acceptance criteria

- [ ] Measured and pinned in Notes with the curl version.
- [ ] An `HttpProtocolHandlerTests` case shows the duplicate line written where curl writes it for a proxy `407` with no `-U`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-10-02: Created.
