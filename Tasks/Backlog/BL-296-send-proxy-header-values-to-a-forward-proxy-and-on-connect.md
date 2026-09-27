---
id: BL-296
title: Send --proxy-header values to a forward proxy and on CONNECT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-183]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-296 — Send --proxy-header values to a forward proxy and on CONNECT

## Goal

`--proxy-header` values reach a forward proxy after the `-H` values, as curl 8.21.0 sends them; the CLI and CONNECT wiring are left to the tasks that own those projects.

## Context

- Found by BL-183: `HttpRequestOptions` has no field for `--proxy-header`, so the forward-proxy head cannot carry them. Adding one changes `Curl.Protocol.Abstractions.UnitLibrary` (ADR-0014), which BL-183's `touches` did not name.
- upstream curl's `Curl_add_custom_headers` sends, for a request to a forward proxy (not tunnelled), the `-H` list and then, when `--proxy-header` was given (`CURLHEADER_SEPARATE`), the proxy-header list.
- The request head is formatted in `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs`; the forward-proxy path is decided in `HttpProtocolHandler.ForwardProxyOf`.
- Measure first with `Record-CurlExchange.ps1` (curl 8.21.0 mingw, ADR-0009): e.g. `-x http://127.0.0.1:18183 --proxy-header "X-P: 1" -H "X-A: 1" http://example.com/`, and with `--proxy-header "Proxy-Connection: close"`.

## Acceptance criteria

- [ ] `HttpRequestOptions` has a `ProxyHeaders` list, documented, defaulting to empty, covered in `Curl.Protocol.Abstractions.UnitTests`.
- [ ] Through a forward proxy, the request head sends `--proxy-header` values where curl 8.21.0 does, byte-equal to measured output recorded in Notes; an origin-form (direct or tunnelled) request never sends them.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Protocol.Http` or `Curl.Protocol.Abstractions`.

## Notes

## Log

- 2026-09-26: Created.
