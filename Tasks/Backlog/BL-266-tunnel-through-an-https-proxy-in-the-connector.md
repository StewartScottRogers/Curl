---
id: BL-266
title: Tunnel through an HTTPS proxy in the connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-212]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-266 — Tunnel through an HTTPS proxy in the connector

## Goal

`TcpConnector` in `Curl.Networking.UnitLibrary` tunnels through a `ProxyKind.Https` proxy the way curl 8.21.0 does - TLS to the proxy host first, then the `CONNECT` exchange BL-212 built over that TLS stream, then (when `ConnectTarget.UseTls` is true) TLS to the target over the tunnel - instead of throwing `NotSupportedException`.

## Context

- Follow-up from BL-212, which made `TcpConnector` tunnel through `ProxyKind.Http` and `ProxyKind.Http10` proxies with `CONNECT` (`Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`, `HttpProxyTunnelOptions.cs`, `TcpConnector.cs`). Today `TcpConnector` throws `NotSupportedException("Tunnelling through a Https proxy is not implemented yet.")` for `ProxyKind.Https`; this task removes that case. SOCKS kinds are BL-213 and stay out of scope.
- Proxy model: `ConnectTarget.Proxy` (`ProxyEndpoint(Kind, Host, Port, Credential)`) in `Curl.Protocol.Abstractions.UnitLibrary`, ADR-0014. TLS comes from the injected TLS provider (`SslStreamTlsProvider` in production); the handshake to the proxy is keyed on the proxy host name, the second handshake on the target host. Never construct a `Socket` or `SslStream` outside the existing dialer/TLS provider seams, so the tests stay off the network.
- Upstream: https://curl.se/docs/manpage.html (`-x`/`--proxy`, "HTTPS proxy"; the `--proxy-cacert`, `--proxy-insecure` family exists but is not parsed yet, so this task uses the transfer's TLS settings for the proxy handshake unless measurement shows otherwise, and records that in Notes) and https://curl.se/libcurl/c/libcurl-errors.html. Checked against curl 8.21.0.
- Measuring: run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, the Windows reference (ADR-0009) - against a TLS loopback proxy (or with `Record-CurlExchange.ps1` at the repository root), record the exact command and the bytes and messages it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `TcpConnector.ConnectAsync` with `ProxyKind.Https` performs TLS to the proxy, sends the same `CONNECT` request bytes BL-212's `HttpProxyTunnel` sends for `ProxyKind.Http` (as measured from curl 8.21.0 through an HTTPS proxy), and, when `UseTls` is true, performs a second TLS handshake to the target host over the tunnel; a named test in `Curl.Networking.UnitTests` asserts each step with fakes, and no test needs `TestCategory=Integration`.
- [ ] A failed TLS handshake to the proxy, a refused `CONNECT` and a failed TLS handshake to the target each return a `ConnectResult.Failed` with the `CurlExitCode` and message measured on curl 8.21.0 (the commands and output recorded in `Notes`), each pinned by a named test.
- [ ] No `ProxyKind.Https` path throws `NotSupportedException`; the XML doc on `TcpConnector` no longer lists `Https` as unsupported.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member for every member this task adds or changes.

## Notes

## Log

- 2026-09-26: Created.
