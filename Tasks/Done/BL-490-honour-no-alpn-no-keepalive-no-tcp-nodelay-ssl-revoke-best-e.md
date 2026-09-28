---
id: BL-490
title: Honour --no-alpn, --no-keepalive, --no-tcp-nodelay, --ssl-revoke-best-effort and --ca-native in the connector
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-489]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-490 — Honour --no-alpn, --no-keepalive, --no-tcp-nodelay, --ssl-revoke-best-effort and --ca-native in the connector

## Goal

The switches BL-489 parses change what the TCP and TLS layers do, as they do in the platform's curl 8.21.0 build: `--no-alpn` sends no ALPN extension, `--no-keepalive` leaves SO_KEEPALIVE off, `--no-tcp-nodelay` leaves Nagle on, `--ssl-revoke-best-effort` tolerates an unknown or offline revocation status on the Schannel build, and `--ca-native` verifies against the operating system's store.

## Context

- Conformance audit 2026-09-28, row 2 (Blocker; "+Networking for Schannel meaning").
- Where they land: `Curl.Networking.UnitLibrary/TcpDialer.cs` and `ITcpDialer.cs` (socket options, behind the `ITcpDialer` seam), `SslStreamTlsProvider.cs` and `TlsClientOptions.cs` (ALPN, revocation, trust store); the mapping from `CommandLineOptions` is `Curl.Console/TlsClientOptionsMapping.cs` and `CurlTransports.cs`.
- Revocation: ADR-0086 (`the-schannel-build-checks-revocation-for-a-cacert-chain`) is the current rule; `--ssl-revoke-best-effort` relaxes it for `X509ChainStatusFlags.RevocationStatusUnknown` and `OfflineRevocation` on the Schannel build. What the OpenSSL build does with it must be measured.
- Not in this task, but not parse-only either (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): `--no-sessionid` and `--ssl-allow-beast` have no `SslStream` control and are honoured through the hand-built TLS client by BL-713; `--styled-output` is BL-736. Name those tasks in the XML doc of the properties BL-489 added until they land.
- ADR-0009: TLS behaviour matches the platform's usual curl build (Schannel on Windows, OpenSSL elsewhere). ADR-0083: thin socket adapters are measured by the integration run, so unit tests go through `ITcpDialer`.

## Acceptance criteria

- [x] Measured first: `curl -v -k --no-alpn https://127.0.0.1:<P>/` and `curl -v -k https://...` through `Record-CurlExchange.ps1 -Tls`, with stderr copied into Notes, so the `-v` ALPN lines with and without the switch are known.
- [x] `--no-alpn` sends no application protocol list: a test on `SslStreamTlsProvider` (or its options mapping) shows `SslClientAuthenticationOptions.ApplicationProtocols` empty, and the `-v` ALPN line disappears as measured.
- [x] Through the `ITcpDialer` seam, tests show SO_KEEPALIVE on by default and off with `--no-keepalive`, and `NoDelay` true by default and false with `--no-tcp-nodelay`.
- [x] On Windows (`[OSCondition(OperatingSystems.Windows)]`) a chain whose only fault is `RevocationStatusUnknown` or `OfflineRevocation` is refused (exit 35 or 60 as today) without `--ssl-revoke-best-effort` and accepted with it; off Windows the measured OpenSSL-build answer is pinned in its own test.
- [x] `--ca-native` verifies against the platform store as measured on each build (no change on Windows, where Schannel already uses it); the test pins each platform's answer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` and `-Library Curl.Console` report 100% line and branch coverage and no failing member.

## Notes

- Scope additions (rule 3; no task in `Doing` named them): `Curl.Cli.UnitLibrary` for the XML docs of `AllowBeast`, `ReuseSessionIds` (BL-713) and `StyledOutput` (BL-736), as Context asks; `Record-CurlExchange.ps1`, extended with `-TlsRootCertificateFile` (a leaf issued by a throwaway private root with no revocation endpoint, root PEM written for `--cacert`) to measure `--ssl-revoke-best-effort`; `Documentation/Planning/Decisions` for ADR-0124.
- Measured 2026-09-28, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tls`, `curl -v -k https://127.0.0.1:48490/` (stderr, TLS part):
  ```
  *   Trying 127.0.0.1:48490...
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  * ALPN: curl offers http/1.1
  * ALPN: server did not agree on a protocol. Uses default.
  * Established connection to 127.0.0.1 (127.0.0.1 port 48490) from 127.0.0.1 port 51395
  ```
  With `--no-alpn` the two `ALPN:` lines are gone and nothing else changes. OpenSSL build (curl 8.18.0 on Ubuntu/WSL): `ALPN: curl offers h2,http/1.1` without the switch, no `ALPN:` line with it.
- Revocation, `--cacert root.pem` with the private root: Schannel exit 60 `schannel: the revocation status is unknown`; with `--ssl-revoke-best-effort` exit 0. OpenSSL build exit 0 either way. `--ca-native`: no change on either build, with or without `--cacert` (identical `SSL Trust Anchors` lines on OpenSSL).
- Decision (ADR-0124, Decided by Claude under Stewart's delegation): offer `http/1.1` on every platform for an `https://` origin handshake; the OpenSSL build's `h2` waits for BL-655/BL-659, since offering `h2` now would let a server pick a protocol Curl cannot speak. `TcpConnector.ApplicationProtocolsFor` picks the list (the target the HTTP handler pools as `https`, not a forward proxy); the provider drops it under `--no-alpn`. Proxy handshakes still offer nothing: not measured, filed as BL-753.
- Keepalive: `SO_KEEPALIVE` with 60 s probe time and interval (curl's `--keepalive-time` default), set before connect in `TcpDialer.ApplySocketOptions`, unit-tested on an unconnected socket so `DialAsync` stays ADR-0083's thin adapter.
- Verified end to end: our `curl -v -k` prints the same two `ALPN:` lines and none with `--no-alpn`; `--cacert` private root exit 60 with the same message, exit 0 with `--ssl-revoke-best-effort`.
- Quality: `Measure-CodeQuality.ps1` 100/100, no failing member, for `Curl.Networking.UnitLibrary` (360 members) and `Curl.Console` (426). The handshake method hit complexity 12 until the ALPN choice moved to `OfferedApplicationProtocols`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --no-alpn drops the http/1.1 ALPN offer, --no-keepalive and --no-tcp-nodelay turn the socket options off, --ssl-revoke-best-effort accepts an unknown or offline revocation status on the Schannel build; --ca-native measured as no change
