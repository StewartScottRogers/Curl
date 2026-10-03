---
id: BL-1254
title: Write the [HTTPS-CONNECT] trace lines of an https:// connect through a proxy, a Unix socket and QUIC
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1254 — Write the [HTTPS-CONNECT] trace lines of an https:// connect through a proxy, a Unix socket and QUIC

## Goal

Curl writes curl 8.21.0's `[HTTPS-CONNECT]` lines (and the https:// origin's `[SETUP]` lines) under `--trace-config https-connect`, `all` and `-vvvv` for an https:// transfer through an HTTP, HTTPS or SOCKS proxy, over `--unix-socket`, and under `--http3`, as it already does for a direct connect.

## Context

- Split from BL-1192 (ADR-0357's BL-1192 amendment), which wrote them for a direct connect only (`TcpConnector.TracingConnectionFilters`, `HttpsConnectFilterTraceEvents`). Proxy connects go through `ConnectThroughProxyAsync`, Unix sockets through `ConnectOverUnixSocketAsync`, QUIC through `ConnectMultiplexedAsync`.
- Measure first with `Record-CurlExchange.ps1` (`-x http://127.0.0.1:<port>` against a `-Script` or `-Tls` recorder; `--unix-socket` needs a server of its own, so extend the recorder). Note where the lines sit beside the CONNECT lines, `[SOCKS]` and `[SETUP] added SSL filter for origin`, and whether `[SETUP] added` is written.

## Acceptance criteria

- [ ] Measured first, stderr in Notes, for a tunnel through an HTTP proxy, a SOCKS5 proxy and a Unix socket.
- [ ] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the measured lines for each route; ADR-0357 gets an amendment.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
