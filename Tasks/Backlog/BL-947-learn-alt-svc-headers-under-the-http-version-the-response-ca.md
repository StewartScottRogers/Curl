---
id: BL-947
title: Learn Alt-Svc headers under the HTTP version the response came over
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-733]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-947 — Learn Alt-Svc headers under the HTTP version the response came over

## Goal

An `Alt-Svc` header is stored in the `--alt-svc` cache under the source ALPN of the HTTP version the response actually came over (`h3` for HTTP/3, `h2` for HTTP/2, `h1` otherwise), as curl 8.21.0 does.

## Context

- Today every Alt-Svc header is stored with source ALPN `h1`: `IAltSvcStore.StoreFromResponse(CurlUrl origin, string altSvcHeader, DateTimeOffset now)` in `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs` carries no version, and `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` (the two `store.StoreFromResponse(...)` calls near lines 1271 and 1290) passes none. The Console store is `Curl.Console/AltSvcTransferCache.cs` over `Curl.Core.UnitLibrary/AltSvc/AltSvcCache.cs`.
- curl 8.21.0 `lib/http.c` passes `k->httpversion` to `Curl_altsvc_parse` (`lib/altsvc.c`): 30 maps to `ALPN_h3`, 20 to `ALPN_h2`, anything else to `ALPN_h1`, and that ALPN is written as the first field of the cache line.
- Measured (curl.se 8.18.0 build with ngtcp2, Windows, 2026-09-29; see BL-733's Notes and ADR-0226): a first run over HTTP/2 to `www.google.com` wrote `h2 www.google.com 443 h3 www.google.com 443 ...`; a second run over HTTP/3 added `h3 www.google.com 443 h3 www.google.com 443 ...`.
- Consequence today: `--http3-only` never finds an entry learned from a response it received (BL-733 looks entries up by the version option, so it looks for `h3` and the file only has `h1`), and `--http2` (and the default off Windows) writes `h1` where curl writes `h2`.
- ADRs: ADR-0175 (cache file format), ADR-0208 (Alt-Svc seams), ADR-0214, ADR-0226 (BL-733's version handling).

## Acceptance criteria

- [ ] `IAltSvcStore.StoreFromResponse` (or its replacement) receives the HTTP version of the response, and `HttpProtocolHandler` passes the version the response came over at both call sites; `Curl.Protocol.Http.UnitTests` pins, through `Fakes/ScriptedAltSvcStore.cs`, that an HTTP/2 response reports HTTP/2 and an HTTP/3 response reports HTTP/3.
- [ ] The store maps HTTP/3 to source ALPN `h3`, HTTP/2 to `h2` and HTTP/1.x to `h1`, pinned by tests in the library that owns the mapping.
- [ ] `Curl.Console.UnitTests` pins the `--alt-svc` file line beginning `h2 <host> <port> ...` after an HTTP/2 transfer and `h3 <host> <port> ...` after an HTTP/3 transfer, and a test pins that a `--http3-only` transfer finds an entry learned from an earlier HTTP/3 response.
- [ ] `dotnet build <project> -warnaserror` is clean for every project changed, and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage for each library changed, with no method over complexity 10 or CRAP 30.

## Notes

## Log

- 2026-09-29: Created.
