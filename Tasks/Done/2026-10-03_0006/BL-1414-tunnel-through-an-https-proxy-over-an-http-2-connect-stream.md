---
id: BL-1414
title: Tunnel through an HTTPS proxy over an HTTP/2 CONNECT stream when --proxy-http2 is set (ADR-0408)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1413]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1414 — Tunnel through an HTTPS proxy over an HTTP/2 CONNECT stream when --proxy-http2 is set (ADR-0408)

## Goal

When proxy HTTP/2 is requested with an `https://` proxy, `Curl.Networking.UnitLibrary` offers `h2,http/1.1` to the proxy. If the proxy picks `h2`, it tunnels through an HTTP/2 `CONNECT` stream presented as an `IConnection`; otherwise it falls back to the existing HTTP/1.1 tunnel.

## Context

- ADR-0408 decisions 4-6. ADR-0061 and ADR-0190 cover the HTTP/1.1 tunnel and its ALPN; ADR-0159 covers HTTP/2 over `IConnection`.
- The tunnel goes beside the HTTP/1.1 one in `TcpConnector` and `ConnectTunnelVerboseLines`. HTTP/2 framing comes from `Curl.Http2.UnitLibrary`, which `Curl.Networking.UnitLibrary.csproj` gains a `ProjectReference` to.
- Pin only the `-v` lines BL-1413 measured; pin nothing that was not measured.
- RFC 9113 section 8.5: a CONNECT request carries only `:method` and `:authority`.

## Acceptance criteria

- [x] A connector option asks for proxy HTTP/2. Without it the proxy ALPN stays `http/1.1` and every existing test passes unchanged.
- [x] With the option set and the proxy picking `h2`, a test over a scripted fake connection shows the HEADERS frame carries `:method CONNECT` and `:authority example.test:443`. A `:status 200` reply yields an `IConnection` whose reads and writes travel in that stream's DATA frames.
- [x] A `:status 407` reply ends with the exit code curl gave in BL-1413's measurement. When the proxy picks `http/1.1`, the connector falls back to the HTTP/1.1 CONNECT tunnel.
- [x] The `-v` lines match BL-1413's measured lines for the 200, 407 and fallback cases.
- [x] `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

- Option: `HttpProxyTunnelOptions.ProxyHttp2` (default false). BL-1415 carries `--proxy-http2`
  from the parsed options to it. With it set, the proxy's handshake offers
  `HttpApplicationProtocols.H2ThenHttp11`; without it, `Http11Only` as before.
- The tunnel is `Http2ProxyTunnelConnection` (an `IConnection` over `Curl.Http2`'s
  `Http2Connection`, stream 1). `TcpConnector.OpenHttp2TunnelAsync` sits beside the HTTP/1.1
  tunnel and takes over once the proxy agrees on `h2`. HEADERS carry `:method`, `:authority`,
  then `proxy-authorization` (if any) and `user-agent` (if any). DATA frames carry the
  target's bytes. Writes wait for WINDOW_UPDATE when the window is spent and keep any DATA
  that arrives meanwhile.
- Pinned lines are curl 8.18.0's from BL-1413's measurement, the only measured build with
  nghttp2: `Establish HTTP/2 proxy tunnel to <host:port>`, then
  `CONNECT tunnel established, response 200` before `CONNECT phase completed`. A non-2xx
  reply (407, 403), or none, gives exit 7 `Could not connect to server` and no `<` line.
  `closing connection #0` is written by the transfer, as for every failed connect.
  The fallback keeps the HTTP/1.1 tunnel's existing 8.21.0 wording
  (`Establishing HTTP proxy tunnel to`, `CONNECT phase completed for HTTP proxy`), byte for
  byte as ADR-0408 decision 4 requires. 8.18.0's older wording of those two lines was
  not re-pinned.
- Choices: a refused h2 CONNECT is not retried with a 407 challenge answer, because curl
  8.18.0 failed at once and no authenticated h2 exchange was measured. `--proxy-header`
  lines are not sent on the h2 CONNECT, because none were measured. No GOAWAY is sent when
  the tunnel is disposed; curl sends one, but it puts no bytes on stdout.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` shows 100% line,
  100% branch, worst CRAP 10.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. With --proxy-http2 an h2 HTTPS proxy is tunnelled through an HTTP/2 CONNECT stream; http/1.1 proxies fall back to the HTTP/1.1 CONNECT
