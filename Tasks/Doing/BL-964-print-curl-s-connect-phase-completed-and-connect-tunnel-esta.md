---
id: BL-964
title: Print curl's CONNECT phase completed and CONNECT tunnel established lines for a proxy tunnel
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-872]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-964 — Print curl's CONNECT phase completed and CONNECT tunnel established lines for a proxy tunnel

## Goal

`-v` through an HTTP or HTTPS proxy tunnel (`-p`, or an HTTPS target through a proxy) prints the CONNECT-phase lines curl prints after the CONNECT reply, in curl's order, on each build.

## Context

- Measured in BL-872 (Notes) through an HTTPS proxy tunnel, after `CONNECT: ... negotiated`:
  - Schannel, curl 8.21.0: `* Establishing HTTP proxy tunnel to example.test:80`, `* schannel: renegotiating SSL/TLS connection`, `* schannel: SSL/TLS connection renegotiated`, `* CONNECT phase completed for HTTP proxy`, `* CONNECT tunnel established, response 200`.
  - OpenSSL, curl 8.18.0 (WSL): `* allocate connect buffer`, `* Establish HTTP proxy tunnel to example.test:80`, the proxy's session-ticket lines, `* CONNECT phase completed`, `* CONNECT tunnel established, response 200`. Measure 8.21.0 on OpenSSL if one can be had: the `Establish`/`Establishing` and `for HTTP proxy` differences may be version, not build.
- BL-863 covers `Establishing HTTP proxy tunnel to <host>:<port>` before each CONNECT and the 407-retry lines; this task covers `allocate connect buffer` (OpenSSL), `CONNECT phase completed[ for HTTP proxy]` and `CONNECT tunnel established, response <code>`. Measure a plain HTTP proxy tunnel too, which BL-872 did not.
- Where: `Curl.Networking.UnitLibrary/TcpConnector.cs` `OpenTunnelAsync` (success branch) and `OpenTunnelOverTlsAsync`. Tests beside `ConnectAsync_ThroughAnHttpsProxy_ReportsTheProxysAlpnAfterItsHandshakeAndBeforeTheConnect` in `TcpConnectorTests.HttpsProxy.cs`.
- Real curl: `Record-CurlExchange.ps1` for an HTTP proxy; for an HTTPS proxy on Windows PowerShell 5.1 the recorder's SslStream cannot select ALPN, so BL-872 used a throwaway C# file-based app for Schannel and `openssl s_server` in WSL for OpenSSL.

## Acceptance criteria

- [ ] The measured lines for an HTTP proxy tunnel and an HTTPS proxy tunnel, per build, are in Notes with the curl versions.
- [ ] `TcpConnector` tests pin each line's text and position for both proxy kinds, per build where the builds differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
