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
completed:
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

- [ ] A connector option asks for proxy HTTP/2. Without it the proxy ALPN stays `http/1.1` and every existing test passes unchanged.
- [ ] With the option set and the proxy picking `h2`, a test over a scripted fake connection shows the HEADERS frame carries `:method CONNECT` and `:authority example.test:443`. A `:status 200` reply yields an `IConnection` whose reads and writes travel in that stream's DATA frames.
- [ ] A `:status 407` reply ends with the exit code curl gave in BL-1413's measurement. When the proxy picks `http/1.1`, the connector falls back to the HTTP/1.1 CONNECT tunnel.
- [ ] The `-v` lines match BL-1413's measured lines for the 200, 407 and fallback cases.
- [ ] `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
