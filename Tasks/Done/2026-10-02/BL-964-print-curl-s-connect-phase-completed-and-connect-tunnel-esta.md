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
completed: 2026-10-02
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

- [x] The measured lines for an HTTP proxy tunnel and an HTTPS proxy tunnel, per build, are in Notes with the curl versions.
- [x] `TcpConnector` tests pin each line's text and position for both proxy kinds, per build where the builds differ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-10-02, `curl -v -s -p -x http://127.0.0.1:<port> -m 3 http://example.test/`, the proxy answering `HTTP/1.1 200 Connection established` and a blank line.

- HTTP proxy, Windows, curl 8.21.0 Schannel (`Record-CurlExchange.ps1 -Port 18964` as the proxy): `Trying 127.0.0.1:18964...`, `CONNECT: no ALPN negotiated`, `Establishing HTTP proxy tunnel to example.test:80`, the CONNECT head, `< HTTP/1.1 200 Connection established`, `< `, **`CONNECT phase completed for HTTP proxy`**, **`CONNECT tunnel established, response 200`**, then `Established connection to 127.0.0.1 ...` and the GET.
- HTTP proxy, WSL, curl 8.18.0 OpenSSL 3.5.5 (`nc -l` as the proxy, since WSL cannot reach the Windows recorder): `Trying`, `CONNECT: no ALPN negotiated`, **`allocate connect buffer`**, `Establish HTTP proxy tunnel to example.test:80`, the head, the reply, **`CONNECT phase completed`**, **`CONNECT tunnel established, response 200`**, then `Established connection`.
- HTTPS proxy (BL-872's measurements): Schannel 8.21.0 `CONNECT: ... negotiated`, `Establishing HTTP proxy tunnel to example.test:80`, ..., `schannel: renegotiating SSL/TLS connection`, `schannel: SSL/TLS connection renegotiated`, `CONNECT phase completed for HTTP proxy`, `CONNECT tunnel established, response 200`; OpenSSL 8.18.0 `CONNECT: ... negotiated`, `allocate connect buffer`, `Establish HTTP proxy tunnel to example.test:80`, the proxy's session-ticket lines, `CONNECT phase completed`, `CONNECT tunnel established, response 200`. The `schannel:` and session-ticket lines come from reading the proxy's TLS records, not from the tunnel, and are not this task's.
- No OpenSSL 8.21.0 binary was at hand, so curl's source at tag `curl-8_21_0` was read instead: `lib/http_proxy.c` writes `Establishing %s proxy tunnel to %s` and `lib/cf-h1-proxy.c` `CONNECT%s phase completed for HTTP proxy`, so `Establish`/`Establishing` and the `for HTTP proxy` suffix are the version, not the build. The same file still writes `allocate connect buffer` in `tunnel_init`, once per tunnel filter (a second CONNECT on the same connection goes through `tunnel_reinit`, which writes nothing), though the Schannel 8.21.0 binary prints none.
- Redial in the OpenSSL build (`--proxy-anyauth`, a 407 with `Connection: close`): `allocate connect buffer` before the first CONNECT was measured; the second listener was not up in time for curl's redial, so its repeat after `Connect me again please` is taken from the source (the redial builds a new filter).

Decisions (ADR-0342): every build prints curl 8.21.0's `CONNECT phase completed for HTTP proxy` and `CONNECT tunnel established, response <code>` after the opening `2xx` reply head; `allocate connect buffer` is the OpenSSL build's alone, once per proxy connection before its first CONNECT's lines. `HttpProxyTunnelOptions.MatchesSchannelBuild` (default `OperatingSystem.IsWindows()`) picks the build.

Implementation: `ConnectTunnelVerboseLines.ReportNewProxyConnection` and `ReportTunnelEstablished`; `TcpConnector.OpenTunnelAsync` calls the first before its CONNECT loop and the new `SecureOpenedTunnelAsync` (the success branch, moved out to keep `OpenTunnelAsync` at complexity 10 or less) calls the second. Through an HTTPS proxy the same path runs inside `OpenTunnelOverTlsAsync`, so both proxy kinds get the lines.

Tests: `TcpConnectorTests.Events` `ConnectAsync_ThroughAProxy_ReportsTryingTheProxyAndTheConnectionOpenedToIt` (Schannel and OpenSSL rows), `TcpConnectorTests.HttpsProxy` `ConnectAsync_ThroughAnHttpsProxy_ReportsTheProxysAlpnAfterItsHandshakeAndBeforeTheConnect` (an OpenSSL row added), `TcpConnectorTests.ProxyAuthVerbose` (the Schannel transcripts end with both lines; `ConnectAsync_InTheOpenSslBuild_ReportsAllocateConnectBufferOnEachProxyConnection` and `ConnectAsync_InTheOpenSslBuildOnAKeptOpenConnection_ReportsAllocateConnectBufferOnce`). The transcript tests pin the build explicitly, so they pass whatever the platform: checked by running Curl.Networking.UnitTests with the default forced to the OpenSSL build too.

Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green in all 33 test projects (Curl.Networking.UnitTests 2514 passed, Curl.Console.UnitTests 2099 passed); `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line, 100% branch, 0 failing, worst CRAP 10. One of three measuring runs reported `UdpChannelOpener.OpenFrom` (untouched) at 85.71% line coverage; the next run 100%. Filed as BL-1154.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v through an HTTP or HTTPS proxy tunnel prints CONNECT phase completed for HTTP proxy and CONNECT tunnel established, response <code>, and the OpenSSL build allocate connect buffer per proxy connection
