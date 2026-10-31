---
id: BL-3193
title: Report HTTPAUTH_AVAIL/USED, PROXYAUTH_*, REFERER and auth retries in REDIRECT_COUNT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.Auth.cs, Curl.Engine.UnitTests/TransferInfoAuthAndRefererTests.cs, Libcurl.UnitTests/Info/EasyInfoAuthAndRefererTests.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3193 — Report HTTPAUTH_AVAIL/USED, PROXYAUTH_*, REFERER and auth retries in REDIRECT_COUNT

## Goal

`CURLINFO_HTTPAUTH_AVAIL`, `HTTPAUTH_USED`, `PROXYAUTH_AVAIL`, `PROXYAUTH_USED`, `REFERER` and `REDIRECT_COUNT` report what upstream reports.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 23 (auth, referer, redirects)). From report 23: HTTPAUTH_AVAIL and HTTPAUTH_USED are always 0; they should give the methods the server offered and the one used (basic, digest, and the rest). PROXYAUTH_* the same for a proxy. CURLINFO_REFERER is null even after CURLOPT_REFERER is set. REDIRECT_COUNT should count the authentication retry (digest, CURLAUTH_ANY), as upstream does. The CLI's `-w` variables that read the same values should pick the fix up.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] Tests show HTTPAUTH_AVAIL/USED after a 401 offering Basic and Digest with CURLAUTH_ANY, and after plain Basic, match upstream's bitmasks.
- [ ] Tests show PROXYAUTH_AVAIL/USED after a 407 the same way.
- [ ] A test shows REFERER returns the value set with CURLOPT_REFERER (and the auto-referer value after a redirect with AUTOREFERER).
- [ ] A test shows REDIRECT_COUNT after a digest auth retry equals upstream's value.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- 2026-10-22 (lane 4): First pass, no code changed. `TransferInfoFromReport.WithCounts` already maps `AuthSchemePicked`/`ProxyAuthSchemePicked` to HTTPAUTH_USED/PROXYAUTH_USED, but HTTPAUTH_AVAIL/PROXYAUTH_AVAIL are read only from the *final* response's `WWW-Authenticate`/`Proxy-Authenticate` when it is a 401/407, so after an auth retry that ends 200 they are 0. Upstream ORs every challenge seen into `data->info.httpauthavail` (`Curl_http_input_auth`), so the HTTP handler must carry the offered bits across its own retry onto `TransferReport` (new field, e.g. `AuthSchemesOffered`/`ProxyAuthSchemesOffered`), which is why `HttpProtocolHandler.cs` and `TransferReport.cs` were added to touches. The handler already counts an auth retry in `RedirectsFollowed` (`HttpProtocolHandler.cs` ~3541, report `RedirectCount` ~1465): check the count is right before changing it. CURLINFO_REFERER for a library handle needs `CURLOPT_REFERER` as a fallback when the report carries none; that lands in `ManagedEasyTransfer.cs` (where `Info` is built; the ToolEvents path already falls back), hence its addition to touches.
- 2026-10-22 (lane 4): Measuring: `Record-CurlExchange.ps1` with two `-Response` values and no `-Connections` hung for 30 min (--anyauth 401 then 200); next run should pass `-Connections 2` or a short `-HoldOpenMilliseconds`, and run it with a timeout. Real curl here is 8.22.0 Schannel (debug build).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 8442c290 2677bb42 ed48196a de830169 3639a5e6.
- 2026-10-22: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, held by BL-3195 (Doing); touches widened to it, HttpProtocolHandler.cs and TransferReport.cs
- 2026-10-23: Backlog -> Deferred. Split over cost cap into BL-3256, BL-3257, BL-3258
