---
id: BL-872
title: Print CONNECT: no ALPN negotiated before a tunnel through an HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-753]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-872 — Print CONNECT: no ALPN negotiated before a tunnel through an HTTPS proxy

## Goal

`-v` through an HTTPS proxy tunnel prints curl's `CONNECT: no ALPN negotiated` line after the proxy's handshake and before the CONNECT request, as curl 8.21.0 does on both builds.

## Context

- Measured in BL-753 (Notes): Schannel prints `* CONNECT: no ALPN negotiated` then `* Establishing HTTP proxy tunnel to example.test:80`; OpenSSL prints `* CONNECT: no ALPN negotiated` then `* allocate connect buffer`. Printed with and without `--no-alpn` when the proxy picks no protocol. What it prints when the proxy picks `http/1.1` is not measured: measure it (the recorder's `-Tls` server would need to select ALPN).
- Where: `Curl.Networking.UnitLibrary/TcpConnector.cs` `OpenTunnelOverTlsAsync`; the negotiated protocol is on the handshake report (`TlsHandshakeEvent`). ADR-0190.
- The other CONNECT-phase lines (`Establishing HTTP proxy tunnel`, `CONNECT phase completed`, `CONNECT tunnel established, response 200`) are not printed either; check BL-863 and file them separately if no task covers them.

## Acceptance criteria

- [x] The measured lines, with the proxy agreeing and not agreeing on `http/1.1`, are in Notes per build.
- [x] A `TcpConnector` test pins the line's text and position for a tunnel through an HTTPS proxy.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29, `curl -v --proxy-insecure -p -x https://<proxy> http://example.test/`.

The recorder runs on Windows PowerShell 5.1, whose .NET Framework `SslStream` cannot select an ALPN protocol as a server, so the recorder's `-Tls` proxy cannot agree on `http/1.1`. For Schannel the proxy was a throwaway C# file-based app (`dotnet run alpnproxy.cs`, kept in `%TEMP%`, not committed) that selects `http/1.1` or nothing and answers `200 Connection established`. For OpenSSL it was `openssl s_server -alpn http/1.1` (or no `-alpn`) inside WSL, as the Windows firewall stops WSL from reaching `dotnet.exe` on the host.

Windows, curl 8.21.0 Schannel:

- proxy agrees on `http/1.1`: `* ALPN: curl offers http/1.1`, `* ALPN: server accepted http/1.1`, `* CONNECT: 'http/1.1' negotiated`, `* Establishing HTTP proxy tunnel to example.test:80`.
- proxy agrees on nothing: `* ALPN: curl offers http/1.1`, `* ALPN: server did not agree on a protocol. Uses default.`, `* CONNECT: no ALPN negotiated`, `* Establishing HTTP proxy tunnel to example.test:80`.

Linux (WSL), curl 8.18.0 OpenSSL 3.5.5:

- proxy agrees on `http/1.1`: `... * SSL connection using TLSv1.3 / ...`, `* ALPN: server accepted http/1.1`, the `* Proxy certificate:` block, `*  SSL certificate verification failed, continuing anyway!`, `* CONNECT: 'http/1.1' negotiated`, `* allocate connect buffer`, `* Establish HTTP proxy tunnel to example.test:80`.
- proxy agrees on nothing: the same with `* ALPN: server did not agree on a protocol. Uses default.` and `* CONNECT: no ALPN negotiated`.

So both builds print the same line in the same place: the last line of the proxy's handshake report, before anything about the CONNECT. BL-753 measured that `--no-alpn` still prints `CONNECT: no ALPN negotiated`.

Decisions (a sensible default, no ADR: it only matches measured output):

- `TcpConnector.OpenTunnelOverTlsAsync` reports `CONNECT: '<protocol>' negotiated` or `CONNECT: no ALPN negotiated` on the target's events once the proxy's handshake succeeds, before the CONNECT is written; nothing when the handshake fails.
- It reads the agreed protocol from `ConnectResult.ApplicationProtocol`, which that property's documentation already defines as "the application protocol the TLS handshake agreed with ALPN". `SslStreamTlsProvider` and `HandBuiltTlsProvider` now set it; before, neither did. Nothing else reads it off a provider's result (`SecureWhenAskedAsync` builds its own result), so no other behaviour changes. A provider that does not report its handshake leaves it null, which prints `no ALPN negotiated`.
- The other CONNECT-phase lines are left out: `Establishing HTTP proxy tunnel` is BL-863's; `allocate connect buffer`, `CONNECT phase completed` and `CONNECT tunnel established, response 200` are filed as BL-959.

Tests: `TcpConnectorTests.HttpsProxy.cs` `ConnectAsync_ThroughAnHttpsProxy_ReportsTheProxysAlpnAfterItsHandshakeAndBeforeTheConnect` (both rows; the line follows the resolve and `Trying` lines and is reported while nothing has been written to the proxy) and `ConnectAsync_WhenTheHandshakeToAnHttpsProxyFails_ReportsNoConnectAlpnLine`; `result.ApplicationProtocol` assertions added to the provider ALPN tests. `RecordingTransferEvents` gained `OnInfo`. Build clean with `-warnaserror`; fast tests green (Networking 1577 passed); Networking coverage 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v through an HTTPS proxy tunnel prints CONNECT: 'http/1.1' negotiated or CONNECT: no ALPN negotiated after the proxy's handshake, as curl does on both builds
