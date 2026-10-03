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
completed: 2026-10-02
---
# BL-1254 — Write the [HTTPS-CONNECT] trace lines of an https:// connect through a proxy, a Unix socket and QUIC

## Goal

Curl writes curl 8.21.0's `[HTTPS-CONNECT]` lines (and the https:// origin's `[SETUP]` lines) under `--trace-config https-connect`, `all` and `-vvvv` for an https:// transfer through an HTTP, HTTPS or SOCKS proxy, over `--unix-socket`, and under `--http3`, as it already does for a direct connect.

## Context

- Split from BL-1192 (ADR-0357's BL-1192 amendment), which wrote them for a direct connect only (`TcpConnector.TracingConnectionFilters`, `HttpsConnectFilterTraceEvents`). Proxy connects go through `ConnectThroughProxyAsync`, Unix sockets through `ConnectOverUnixSocketAsync`, QUIC through `ConnectMultiplexedAsync`.
- Measure first with `Record-CurlExchange.ps1` (`-x http://127.0.0.1:<port>` against a `-Script` or `-Tls` recorder; `--unix-socket` needs a server of its own, so extend the recorder). Note where the lines sit beside the CONNECT lines, `[SOCKS]` and `[SETUP] added SSL filter for origin`, and whether `[SETUP] added` is written.

## Acceptance criteria

- [x] Measured first, stderr in Notes, for a tunnel through an HTTP proxy, a SOCKS5 proxy and a Unix socket.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the measured lines for each route; ADR-0357 gets an amendment.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -k -v --trace-config https-connect,setup ... https://example.test/`; fixtures in `%TEMP%\bl1254\<case>`. Each scripted peer closes at the ClientHello, so each ends exit 35.
  - HTTP proxy (`-Script`: read, send `HTTP/1.1 200 Connection established`, read, close; `-x http://127.0.0.1:18454`): `[HTTPS-CONNECT] added`, `connect, init`, `1st attempt uses h2 from wanted versions`, `[SETUP] happy eyeballing to proxy 127.0.0.1:18454`, `Trying`, pair, `[SETUP] added HTTP proxy tunnel filter`, `CONNECT: no ALPN negotiated`, `Establishing HTTP proxy tunnel to example.test:443`, the CONNECT head, pair, the reply head, `CONNECT phase completed for HTTP proxy`, `CONNECT tunnel established, response 200`, `[SETUP] added SSL filter for origin`, `schannel: disabled automatic use of client certificate`, `ALPN: curl offers http/1.1`, pair, `schannel: failed to receive handshake, SSL/TLS connection failed`, `connect, all attempts failed`, `connect -> 35, done=0`, `closing connection #0`. Under `https-connect` alone the same without the `[SETUP]` lines.
  - SOCKS5h (`-Script`: 05 00, then 05 00 00 01 7f000001 01bb; `-x socks5h://127.0.0.1:18455`): the three init lines, `[SETUP] happy eyeballing to origin 127.0.0.1:18455`, `Trying`, pair, `[SETUP] added SOCKS filter to example.test:443`, two pairs, `Opened SOCKS connection from 127.0.0.1 port N to example.test port 443 (via 127.0.0.1 port 18455)`, `[SETUP] added SSL filter for origin`, the `schannel:` and `ALPN` lines, pair, the handshake failure, `all attempts failed`, `connect -> 35`. With `socks5://` the local resolve of example.test failed after four pairs: exit 6.
  - Unix socket (`-UnixSocket`, plain HTTP server; `--unix-socket <path>`): the three init lines, `[SETUP] happy eyeballing to origin <whole path>:0`, `Trying <path cut to 45>:0...`, pair, `[SETUP] added SSL filter for origin`, `schannel:`/`ALPN` lines, two pairs, `SEC_E_INVALID_TOKEN`, `all attempts failed`, `connect -> 35`.
  - HTTPS proxy (`-Tls -Response 'HTTP/1.1 200 Connection established\r\n\r\n'`, `--proxy-insecure`): init lines, `[SETUP] happy eyeballing to proxy`, `Trying`, pair, `[SETUP] added SSL filter for HTTP proxy`, `[SETUP] added HTTP proxy tunnel filter`, the proxy handshake with two pairs, the CONNECT, pair, the reply, `[SETUP] added SSL filter for origin`, the origin handshake failure, exit 35.
  - Plain `http://` with `--trace-config setup`: through socks5h `[SETUP] added`, `happy eyeballing to origin 127.0.0.1:18457`, `Trying`, `added SOCKS filter`, `Opened SOCKS connection`, `Established connection`, the removal; over a Unix socket `[SETUP] added`, `happy eyeballing to origin <path>:0`, `Trying`, `Established connection`, the removal. Curl wrote neither before.
- Delivered (ADR-0357's BL-1254 amendment): `TcpConnector.SetupAndDnsFilterEvents` now builds the `[HTTPS-CONNECT]`, `[DNS]` and `[SETUP]` filters for the direct, proxy and Unix routes, each passing its own DNS and setup filter. `HttpsConnectFilterTraceEvents` writes a pair after a request head (the CONNECT) and two before `Opened SOCKS connection`. The setup filter is now traced through SOCKS proxies (to origin) and over Unix sockets (to `<path>:0`), with `SslFilterAddedLine` for an https:// origin on both.
- Decisions: pair counts fixed to the loopback measurement, as in BL-1192; a failed SOCKS handshake writes none of its pairs. QUIC is not done: the reference build has no HTTP/3, so it cannot be measured here (filed BL-1262). An HTTPS proxy's own `[SETUP]` lines are a gap that already existed (filed BL-1261); through it the `[HTTPS-CONNECT]` lines are written.
- Tests: `HttpsConnectFilterTraceEventsTests` (request head pair, SOCKS pairs), `TcpConnectorTests.HttpsConnectTraceRoutes` (HTTP proxy in full, its exit 35, SOCKS5h, SOCKS setup for http://, Unix https:// and http://, refused Unix exit 7), `CurlCommandRunnerHttpsConnectTraceTests` (HTTP proxy, SOCKS5h, Unix socket through the production composition). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members; Curl.Console's production code is unchanged.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. An https:// connect through an HTTP, HTTPS or SOCKS proxy or over a Unix socket writes curl's [HTTPS-CONNECT] lines and the origin's [SETUP] lines
