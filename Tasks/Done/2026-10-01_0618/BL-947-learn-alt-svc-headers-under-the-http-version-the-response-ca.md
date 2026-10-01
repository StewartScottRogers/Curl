---
id: BL-947
title: Learn Alt-Svc headers under the HTTP version the response came over
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-733]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0226-alt-svc-uses-h2-and-h3-alternatives-as-curl-se-s-build-looks-them-up.md]
requirement: none
created: 2026-09-29
completed: 2026-10-01
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

- [x] `IAltSvcStore.StoreFromResponse` (or its replacement) receives the HTTP version of the response, and `HttpProtocolHandler` passes the version the response came over at both call sites; `Curl.Protocol.Http.UnitTests` pins, through `Fakes/ScriptedAltSvcStore.cs`, that an HTTP/2 response reports HTTP/2 and an HTTP/3 response reports HTTP/3.
- [x] The store maps HTTP/3 to source ALPN `h3`, HTTP/2 to `h2` and HTTP/1.x to `h1`, pinned by tests in the library that owns the mapping.
- [x] `Curl.Console.UnitTests` pins the `--alt-svc` file line beginning `h2 <host> <port> ...` after an HTTP/2 transfer and `h3 <host> <port> ...` after an HTTP/3 transfer, and a test pins that a `--http3-only` transfer finds an entry learned from an earlier HTTP/3 response.
- [x] `dotnet build <project> -warnaserror` is clean for every project changed, and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage for each library changed, with no method over complexity 10 or CRAP 30.

## Notes

- Plan: `IAltSvcStore.StoreFromResponse` gains a `Version responseVersion` parameter (System.Version, the type `HttpStatusLine.Version` and `TransferResult.HttpVersion` already use, so no new abstraction). `HttpResponseHeadReader.HeaderReceived` now passes the status line of the head each header belongs to, and `HttpProtocolHandler.StoreAltSvc` hands its version on. The status line is the truth of what the response came over (HTTP/2 and HTTP/3 streams synthesize `HTTP/2` and `HTTP/3` status lines), so no connection state had to be threaded through.
- The context named two `StoreFromResponse` call sites; only one is the Alt-Svc store (the other, near it, is the cookie store), so only that one changed.
- The mapping lives in `Curl.Console` (`AltSvcTransferCache.SourceAlpnOf`): major 3 -> `h3`, major 2 -> `h2`, anything else -> `h1`, matching curl 8.21.0 `lib/altsvc.c`. Pinned by `AltSvcTransferCacheTests` and end to end by three new `CurlCommandRunnerAltSvcVersionTests` (HTTP/2 writes `h2 ...`, HTTP/3 writes `h3 ...`, `--http3-only` follows an entry an earlier HTTP/3 response taught). No ADR: the behaviour is curl's, measured in BL-733's Notes; no choice was left open.
- Measure-CodeQuality: Abstractions, Http and Console each 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Added ADR-0226 to `touches`: its Consequences named this gap as open, which this task closes; no task in Doing on `origin/work/dark-factory` names it.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Alt-Svc headers are learned under h3, h2 or h1 by the version the response came over; --http3-only uses entries HTTP/3 responses taught
