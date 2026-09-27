---
id: BL-335
title: Send --proxy-header values on the CONNECT request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-296]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-335 — Send --proxy-header values on the CONNECT request

## Goal

The CONNECT request curl sends to an HTTP proxy (https URL, `-p`) carries the `--proxy-header` values where curl 8.21.0 does, byte-equal to measured output.

## Context

- Found by BL-296: `HttpRequestOptions.ProxyHeaders` now reaches a forward-proxy request head, but the tunnel is built by `Curl.Networking.UnitLibrary/HttpProxyTunnel.BuildConnectRequest` from `HttpProxyTunnelOptions` (user agent and credential encoding only), which has no way to receive them.
- Decide the seam (ADR): extend `HttpProxyTunnelOptions`, or carry the headers on `ProxyEndpoint` / `ConnectTarget` in Abstractions. Pick the smallest change that keeps protocol handlers off the network.
- upstream curl sends proxy headers on CONNECT via `Curl_add_custom_headers(data, TRUE, ...)`; with `--proxy-header` given (CURLHEADER_SEPARATE) only the proxy list is sent there, and `-H` values are not. Measure with `Record-CurlExchange.ps1` (ADR-0009), e.g. `-x http://127.0.0.1:18335 -p --proxy-header "X-P: 1" -H "X-A: 1" http://example.com/`, plus `--proxy-header "User-Agent: x"`, `"Host: h"` and `"Proxy-Connection: close"`.

## Acceptance criteria

- [ ] The CONNECT request with `--proxy-header` values is byte-equal to measured curl 8.21.0 output recorded in Notes, including which of curl's own CONNECT headers a proxy header replaces, covered in `Curl.Networking.UnitTests`.
- [ ] `-H` values are never sent on CONNECT (measured).
- [ ] The seam is recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-27: Created.
