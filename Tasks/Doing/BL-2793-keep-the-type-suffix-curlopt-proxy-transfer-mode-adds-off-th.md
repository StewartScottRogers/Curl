---
id: BL-2793
title: Keep the ;type= suffix CURLOPT_PROXY_TRANSFER_MODE adds off the effective URL of a forwarded FTP transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Engine.UnitLibrary/ProxyTransferModeUrl.cs, Curl.Engine.UnitTests/ProxyTransferModeUrlTests.cs, Curl.Engine.UnitLibrary/ProxyTransferModeType.cs, Curl.Engine.UnitTests/ProxyTransferModeTypeTests.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2793 — Keep the ;type= suffix CURLOPT_PROXY_TRANSFER_MODE adds off the effective URL of a forwarded FTP transfer

## Goal

A handle with `CURLOPT_PROXY_TRANSFER_MODE` that fetches `ftp://h/f` through an HTTP proxy sends `GET ftp://h/f;type=i HTTP/1.1` but reports `CURLINFO_EFFECTIVE_URL` (and `-w %{url_effective}`) as `ftp://h/f`, as curl 8.22.0 does.

## Context

Filed by BL-2718. `Curl.Engine`'s `ProxyTransferModeUrl.Of` puts the `;type=a|i` suffix on the context's `Url`, so the request line is right (upstream tests 549 and 550), but the HTTP handler also derives the effective URL from that `Url`. Upstream's `http_target` in `lib/http.c` appends the suffix only to the request target it builds, leaving `data->state.url` untouched. A likely shape: carry the suffix as a request-target option on `HttpRequestOptions` (or an absolute-target override) that the HTTP handler appends when it writes the forward-proxy request line, and drop the URL rewrite from `ProxyTransferModeUrl`.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Http.UnitTests` (or `Curl.Engine.UnitTests`) pins that a forwarded `ftp` transfer under the proxy transfer mode sends `;type=i` on the request line and reports the effective URL without it.
- [x] Upstream cases 549 and 550 still pass on the ratchet.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Measured first: the effective URL (`easy.Info.EffectiveUrl`, so `CURLINFO_EFFECTIVE_URL`) already came from the handle's URL text, not `TransferContext.Url`, so it was already right. The suffix did leak wherever the URL itself is reused: a relative `Location: ?x` resolved to `ftp://h/moo/549;type=i?x` where curl gives `ftp://h/moo/549?x`.
- Fix (the shape Context suggested): new `HttpRequestOptions.ProxyTransferType` (`a`/`i` or null). The engine sets it (`ProxyTransferModeType.Of`, renamed from `ProxyTransferModeUrl` since it no longer rewrites a URL) and leaves `Url` alone. `HttpRequestHeadFormatter.AbsoluteForm` appends `;type=` to an `ftp` path before the query unless the path already names its type, per hop, as `http_target` does.
- Touches widened to `HttpRequestOptions.cs` (the new contract property), `TransferContextFactory.cs` (sets it) and the renamed engine files; no other task was in Doing on `origin/work/dark-factory`.
- Tests: `HttpRequestHeadFormatterTests.ProxyTransferType.cs` (target shapes), `ProxyTransferModeTypeTests` (type choice, plus end-to-end effective URL and redirect URL through the easy handle). Measure-CodeQuality: Engine, Http and Abstractions 100% line and branch, no failing member.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 88159cf1 32bc0832 4272a6b9.
