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
completed: 2026-09-27
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

- [x] `HttpRequestOptions` has a `ProxyHeaders` list, documented, defaulting to empty, covered in `Curl.Protocol.Abstractions.UnitTests`.
- [x] Through a forward proxy, the request head sends `--proxy-header` values where curl 8.21.0 does, byte-equal to measured output recorded in Notes; an origin-form (direct or tunnelled) request never sends them.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Protocol.Http` or `Curl.Protocol.Abstractions`.

## Notes

- Delivered: `HttpRequestOptions.ProxyHeaders` (Abstractions, default empty); `HttpRequestHeadFormatter.Format` parses them with `HttpCustomHeader` only when `forwardProxy` is true, lets them (like `-H`) remove or replace curl's `Proxy-Connection: Keep-Alive`, and appends them after the `-H` values and before the body headers under `AppendCustomHeaders`' rules. Tests: `HttpRequestOptionsTests` (default and round trip), `HttpRequestHeadFormatterTests.Format_ForwardProxyWithProxyHeaders_SendsMeasuredHead` (6 rows), `Format_ForwardProxyWithProxyHeadersAndBody_SendsThemBeforeTheBodyHeaders`, `Format_ProxyHeadersWithoutForwardProxy_SendsNone`, `HttpProtocolHandlerTests.ExecuteAsync_HttpThroughProxyWithProxyHeaders_SendsThemAfterTheCustomHeaders`; the tunnelled-proxy test now sets `ProxyHeaders` and still expects an origin-form head without them.
- Scope: the Goal leaves the CLI and CONNECT wiring to the tasks that own those projects, so the title's "and on CONNECT" is filed as BL-347 (Networking tunnel) and the command-line parsing as BL-346 (Cli, Console). No design decision beyond measured behaviour was needed, so no ADR.
- Measured with `Record-CurlExchange.ps1 -Port 18296` against curl 8.21.0 (mingw, Schannel), 2026-09-27; `-x http://127.0.0.1:18296` unless noted, `http://example.com/`:
  - `--proxy-header "X-P: 1" -H "X-A: 1"` -> `Host`, `User-Agent`, `Accept`, `Proxy-Connection: Keep-Alive`, `X-A: 1`, `X-P: 1`.
  - `--proxy-header "Proxy-Connection: close"` -> curl's `Proxy-Connection` dropped; `--proxy-header "Proxy-Connection: close" -H "X-A: 1"` -> `... Accept`, `X-A: 1`, `Proxy-Connection: close` (sent in the proxy-header slot).
  - `-H "Proxy-Connection: close" --proxy-header "X-P: 1"` -> `-H` still replaces curl's: `... Accept`, `Proxy-Connection: close`, `X-P: 1`.
  - `--proxy-header "User-Agent: pu" "X-E:" "X-S;" "Host: ph"` -> curl's `User-Agent: curl/8.21.0` kept, then `User-Agent: pu`, `X-S:`; `X-E:` and `Host: ph` not sent.
  - `-H "Host:" --proxy-header "Host: ph"` -> no curl `Host` line; `Host: ph` sent after `Proxy-Connection: Keep-Alive`.
  - `--proxy-header "Proxy-Connection:" --proxy-header "Authorization: z" -u a:b` -> `Authorization: Basic YTpi` kept, no `Proxy-Connection`, `Authorization: z` last.
  - `--proxy-header "Content-Type: x" --proxy-header "Content-Length: 9" -d xy` -> both sent after `Proxy-Connection`, then curl's `Content-Length: 2` and `Content-Type: application/x-www-form-urlencoded`: body headers are not overridden.
  - `--proxy-header "X-P: 1" -H "X-P: 2" -d xy` -> `X-P: 2`, `X-P: 1`, then the body headers.
  - `--proxy-header "Cookie: c=1" -b a=b -H "Proxy-Connection;"` -> `Cookie: a=b`, `Proxy-Connection:`, `Cookie: c=1`.
  - No `-x`: `--proxy-header "X-P: 1" http://127.0.0.1:18296/` -> the default head, nothing added.
- Gates: `dotnet build -warnaserror` 0 warnings, 0 errors; fast tests all pass (Http 734, Abstractions 371); `Measure-CodeQuality.ps1`: Http 100/100, 279 members, 0 failing, worst CRAP 10; Abstractions 100/100, 96 members, 0 failing.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --proxy-header values (HttpRequestOptions.ProxyHeaders) reach a forward-proxy request head after the -H values, byte-equal to curl 8.21.0; CLI and CONNECT filed as BL-346, BL-347
