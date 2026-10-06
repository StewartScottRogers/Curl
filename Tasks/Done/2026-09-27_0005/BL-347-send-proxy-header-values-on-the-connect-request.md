---
id: BL-347
title: Send --proxy-header values on the CONNECT request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-296]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-347 — Send --proxy-header values on the CONNECT request

## Goal

The CONNECT request curl sends to an HTTP proxy (https URL, `-p`) carries the `--proxy-header` values where curl 8.21.0 does, byte-equal to measured output.

## Context

- Found by BL-296: `HttpRequestOptions.ProxyHeaders` now reaches a forward-proxy request head, but the tunnel is built by `Curl.Networking.UnitLibrary/HttpProxyTunnel.BuildConnectRequest` from `HttpProxyTunnelOptions` (user agent and credential encoding only), which has no way to receive them.
- Decide the seam (ADR): extend `HttpProxyTunnelOptions`, or carry the headers on `ProxyEndpoint` / `ConnectTarget` in Abstractions. Pick the smallest change that keeps protocol handlers off the network.
- upstream curl sends proxy headers on CONNECT via `Curl_add_custom_headers(data, TRUE, ...)`; with `--proxy-header` given (CURLHEADER_SEPARATE) only the proxy list is sent there, and `-H` values are not. Measure with `Record-CurlExchange.ps1` (ADR-0009), e.g. `-x http://127.0.0.1:18335 -p --proxy-header "X-P: 1" -H "X-A: 1" http://example.com/`, plus `--proxy-header "User-Agent: x"`, `"Host: h"` and `"Proxy-Connection: close"`.

## Acceptance criteria

- [x] The CONNECT request with `--proxy-header` values is byte-equal to measured curl 8.21.0 output recorded in Notes, including which of curl's own CONNECT headers a proxy header replaces, covered in `Curl.Networking.UnitTests`.
- [x] `-H` values are never sent on CONNECT (measured).
- [x] The seam is recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Touches: added `Documentation/Planning/Decisions` for ADR-0077 and its index line; no
  task in Doing names it (Doing held BL-290 on Curl.Output and BL-301 on Curl.Core).
- Seam (ADR-0077): `HttpProxyTunnelOptions.ProxyHeaders`, an init-only list, filled by
  `CurlComposition.CreateProxyTunnelOptions` from `CommandLineOptions.ProxyHeaders`.
  Abstractions untouched, so its projects in `touches` were not changed.
- New `Curl.Networking/HttpProxyTunnelHeader` reads a value by curl's CONNECT rules, which
  differ from `-H`'s (`X-E;` sends `X-E: ` here, `X-E:` on a request).
- Measured 2026-09-27, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1`,
  `curl -s -x http://127.0.0.1:<port> -p <args> http://example.com/`, proxy answers 403.
  Request lines after `CONNECT example.com:80 HTTP/1.1` (CRLF each):
  - `--proxy-header "X-P: 1" -H "X-A: 1"` -> Host, User-Agent: curl/8.21.0, Proxy-Connection: Keep-Alive, `X-P: 1` (no X-A)
  - `--proxy-header "User-Agent: x"` -> Host, Proxy-Connection: Keep-Alive, `User-Agent: x`
  - `--proxy-header "Host: h"` -> User-Agent, Proxy-Connection: Keep-Alive, `Host: h`
  - `--proxy-header "Proxy-Connection: close"` -> Host, User-Agent, `Proxy-Connection: close`
  - `-U u:p --proxy-header "Proxy-Authorization: Z" --proxy-header "X-B: 2"` -> Host, User-Agent, Proxy-Connection, `Proxy-Authorization: Z`, `X-B: 2` (curl's Basic credential dropped)
  - `User-Agent:`, `X-E;`, `Proxy-Connection;` -> Host, `X-E: `, `Proxy-Connection: `
  - `X-P:   spaced  `, `X-T:<tab>t`, `X-N:1` -> Host, UA, P-C, `X-P: spaced  `, `X-T: t`, `X-N: 1`
  - `X-F; junk`, `Bogus`, `: novalue`, `X-G:` -> nothing added, nothing removed
  - `-U u:p` with `Host:`, `Proxy-Authorization:`, `Proxy-Connection:` -> only `User-Agent: curl/8.21.0`
  - `X-D: 1`, `x-d: 2`, `user-agent: lower` -> Host, P-C, all three verbatim (case-insensitive override, duplicates kept)
  - `Host;`, `Content-Length: 5`, `Connection: close` -> UA, P-C, `Host: `, both others verbatim
  - `Host; junk`, `User-Agent`, `Proxy-Connection;x` -> only `User-Agent: curl/8.21.0` (a name before `;` overrides even when nothing is sent; bare name does not)
  - `X-S;: v`, `X-C:v;w`, `X-W: ` -> only `X-C: v;w` (first of `:`/`;` ends the name)
  - `-H "User-Agent: hx" -A ag --proxy-header "X-P: 1"` -> `User-Agent: ag`; `-H "Host: hh" -H "Proxy-Connection: close"` -> curl's defaults unchanged
  - `--proxy1.0` and an https URL (CONNECT :443) carry `X-P: 1` the same way.
- Tests: `HttpProxyTunnelTests.BuildConnectRequest_WithProxyHeaders_MatchesCurl` (11 rows) and two more in
  Curl.Networking.UnitTests; one each in `CurlTransportsTests` and `CurlCompositionProxyTests`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. CONNECT through an HTTP proxy carries --proxy-header values byte-equal to curl 8.21.0, replacing the CONNECT headers they name; -H never reaches it (ADR-0077)
