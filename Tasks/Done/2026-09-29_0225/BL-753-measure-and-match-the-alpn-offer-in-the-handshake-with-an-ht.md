---
id: BL-753
title: Measure and match the ALPN offer in the handshake with an HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-490]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-753 — Measure and match the ALPN offer in the handshake with an HTTPS proxy

## Goal

The TLS handshake with an HTTPS proxy (`-x https://...`, and an HTTPS forward proxy) offers through ALPN exactly what the platform's curl 8.21.0 build offers there, so `-v` prints the same `ALPN:` lines for the proxy handshake.

## Context

- BL-490 / ADR-0124 made the origin handshake of an `https://` transfer offer `http/1.1` (none under `--no-alpn`) but left the proxy handshakes offering nothing, because curl's proxy ALPN was not measured.
- Where: `Curl.Networking.UnitLibrary/TcpConnector.cs` (`ApplicationProtocolsFor`, and the `AuthenticateAsync` call for the proxy passing `applicationProtocols: []`).
- Measure with `Record-CurlExchange.ps1 -Tls` as the proxy (`curl -v --proxy-insecure -x https://127.0.0.1:P http://example.test/`, and with `-p` to tunnel), on Windows (Schannel) and through WSL (OpenSSL, `-ListenAddress`), with and without `--no-alpn`.

## Acceptance criteria

- [x] The measured `-v` lines for the proxy handshake, per build, are copied into Notes.
- [x] Tests on `TcpConnector` pin the protocols offered to an HTTPS proxy and an HTTPS forward proxy as measured, and with `--no-alpn` if curl applies it to the proxy.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 with `Record-CurlExchange.ps1 -Tls` as the proxy, `curl -v --proxy-insecure -x https://<proxy>:18753 http://example.test/`.

Windows, curl 8.21.0 Schannel:

- forward proxy: `* ALPN: curl offers http/1.1`, `* ALPN: server did not agree on a protocol. Uses default.`
- forward proxy, `--no-alpn`: no `ALPN:` line.
- tunnel (`-p`): `* ALPN: curl offers http/1.1`, `* ALPN: server did not agree on a protocol. Uses default.`, `* CONNECT: no ALPN negotiated`, `* Establishing HTTP proxy tunnel to example.test:80`.
- tunnel, `--no-alpn`: no `ALPN:` line; `* CONNECT: no ALPN negotiated` is still printed.

Linux (WSL), curl 8.18.0 OpenSSL (`-ListenAddress 172.26.96.1`, `-Curl wsl.exe`):

- forward proxy: `* ALPN: curl offers http/1.1` before `* TLSv1.3 (OUT), TLS handshake, Client hello (1):`, then `* ALPN: server did not agree on a protocol. Uses default.` after `* SSL connection using ...`.
- forward proxy, `--http2`: the same `* ALPN: curl offers http/1.1` (the proxy's offer ignores the version options).
- forward proxy, `--no-alpn`: no `ALPN:` line.
- tunnel (`-p`): as the forward proxy, then `* CONNECT: no ALPN negotiated`, `* allocate connect buffer`.
- tunnel, `--no-alpn`: no `ALPN:` line; `* CONNECT: no ALPN negotiated` is still printed.

Decisions (ADR-0190, decided by Claude under Stewart's delegation):

- Both proxy handshakes (tunnel and forward) offer `HttpApplicationProtocols.Http11Only` whatever `--http2` says. `ApplicationProtocolsFor` returns it for a forward proxy; `OpenTunnelOverTlsAsync` passes it for the tunnel's proxy.
- `--no-alpn` does apply to the proxy. The providers already drop the offer when `UseAlpn` is off, and the provider-level tests (`SslStreamTlsProviderTests.Alpn.cs`, `HandBuiltTlsProviderTests`) pin that. What is missing is `Curl.Console`'s `TlsClientOptionsMapping.ProxyFromCommandLine`, which does not carry `--no-alpn` to the proxy's options. `Curl.Console` is held by BL-631 in Doing, so the fix is filed as BL-871 rather than widening this task. Until then, `--no-alpn` with an HTTPS proxy still offers `http/1.1` to the proxy. Before this task the proxy was offered nothing in every case, so the common case is now right and only that rarer one stays wrong.
- `Documentation/Planning/Decisions` was added to `touches` for ADR-0190 and the ADR-0124 amendment note. No task in Doing names it.
- The `CONNECT: no ALPN negotiated` line is not an `ALPN:` line and Curl does not print it or any other CONNECT-phase line yet. It is filed as BL-872.

Tests added in `TcpConnectorTests.cs`: `ApplicationProtocolsFor_AnHttpsForwardProxy_OffersHttp11WhateverTheHttpList`, `ConnectAsync_ToAnHttpsForwardProxy_OffersHttp11ThroughAlpnUnderHttp2`, and `ConnectAsync_ThroughAnHttpsProxy_OffersHttp11ThroughAlpnInTheProxysHandshakeUnderHttp2`. Build clean with `-warnaserror`, fast tests green (Networking 1372 passed), and Networking coverage is 100% line and branch with 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The TLS handshake with an HTTPS proxy, tunnel or forward, offers http/1.1 through ALPN as curl 8.21.0 does on both builds (ADR-0190)
