---
id: BL-1255
title: Write the [HTTP-PROXY], [H1-PROXY] and proxy TLS trace lines of a CONNECT tunnel through an HTTPS proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1255 — Write the [HTTP-PROXY], [H1-PROXY] and proxy TLS trace lines of a CONNECT tunnel through an HTTPS proxy

## Goal

Curl writes curl 8.21.0's `[SETUP]`, `[HTTP-PROXY]` and `[H1-PROXY]` lines (and any proxy TLS filter lines its `--trace-config` groups name) around a CONNECT tunnel through an HTTPS proxy (`-x https://`), as BL-1193 does through a plain HTTP proxy.

## Context

- Split from BL-1193 (ADR-0357's BL-1193 amendment), which writes them through `Http` and `Http10` proxies only: `HttpProxyTunnelTrace` is created in `TcpConnector.OpenPlainTunnelAsync`; `OpenTunnelOverTlsAsync` passes none, and `TracesSetupFilterThrough` leaves the setup filter off for an `Https` proxy.
- Measure first with `Record-CurlExchange.ps1`; the recorder's `-Tls` mode serves TLS but not a CONNECT followed by the tunnel's bytes, so extend it to play an HTTPS proxy (TLS, then the `-Script` steps). Where `[SETUP] added SSL filter for proxy`, the proxy handshake's lines and `CONNECT: 'http/1.1' negotiated` fall relative to the tunnel lines is what matters.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (a successful transfer and a refused connect, `--trace-config proxy,setup` and `all`), stderr in Notes.
- [ ] Tests in `Curl.Networking.UnitTests` pin the lines through an `Https` proxy and that none appears without its component; ADR-0357 gets an amendment for any volatile value.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for Curl.Networking.UnitLibrary.

## Notes

## Log

- 2026-10-02: Created.
