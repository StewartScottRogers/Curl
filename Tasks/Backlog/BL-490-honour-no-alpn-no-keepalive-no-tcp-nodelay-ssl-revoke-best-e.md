---
id: BL-490
title: Honour --no-alpn, --no-keepalive, --no-tcp-nodelay, --ssl-revoke-best-effort and --ca-native in the connector
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-489]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
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

- [ ] Measured first: `curl -v -k --no-alpn https://127.0.0.1:<P>/` and `curl -v -k https://...` through `Record-CurlExchange.ps1 -Tls`, with stderr copied into Notes, so the `-v` ALPN lines with and without the switch are known.
- [ ] `--no-alpn` sends no application protocol list: a test on `SslStreamTlsProvider` (or its options mapping) shows `SslClientAuthenticationOptions.ApplicationProtocols` empty, and the `-v` ALPN line disappears as measured.
- [ ] Through the `ITcpDialer` seam, tests show SO_KEEPALIVE on by default and off with `--no-keepalive`, and `NoDelay` true by default and false with `--no-tcp-nodelay`.
- [ ] On Windows (`[OSCondition(OperatingSystems.Windows)]`) a chain whose only fault is `RevocationStatusUnknown` or `OfflineRevocation` is refused (exit 35 or 60 as today) without `--ssl-revoke-best-effort` and accepted with it; off Windows the measured OpenSSL-build answer is pinned in its own test.
- [ ] `--ca-native` verifies against the platform store as measured on each build (no change on Windows, where Schannel already uses it); the test pins each platform's answer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` and `-Library Curl.Console` report 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
