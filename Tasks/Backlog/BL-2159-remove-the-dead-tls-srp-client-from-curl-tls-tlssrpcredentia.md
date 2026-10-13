---
id: BL-2159
title: Remove the dead TLS-SRP client from Curl.Tls: TlsSrpCredentials, Tls12ClientSettings.SrpCredentials, the SRP key exchange, SrpClient, SrpGroup, SrpExtension, Tls12SrpParameters and the SRP suites
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2152]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2159 — Remove the dead TLS-SRP client from Curl.Tls: TlsSrpCredentials, Tls12ClientSettings.SrpCredentials, the SRP key exchange, SrpClient, SrpGroup, SrpExtension, Tls12SrpParameters and the SRP suites

## Goal

Curl.Tls carries no TLS-SRP code, since curl 8.22.0 dropped TLS-SRP (ADR-0480) and BL-2152 removed the only caller.

## Context

BL-2152 removed every Curl.Networking path that set `Tls12ClientSettings.SrpCredentials`, so the hand-built TLS-SRP client is unreachable. Remove `TlsSrpCredentials.cs`, `Tls12ClientSettings.SrpCredentials` and its suite filter, `Tls12ClientHelloBuilder`'s srp extension, `Tls12ClientHandshake.AgreeSrp`, `Tls12KeyExchange.Srp`, `Tls12SrpParameters`, the nine SRP rows of `Tls12CipherSuite`, `SrpClient`, `SrpGroup`, `SrpExtension`, and their tests (`Tls12SrpHandshakeTests`, `SrpClientTests`, `Tls12TestServer`'s SRP path, `TlsExtensionCodecTests.SrpRoundTripsTheUserName`), and the `Curl.Tls.UnitLibrary/CLAUDE.md` mention. Keep `TlsExtensionType.Srp` only if a ClientHello profile still names extension 12 in its order (`ClientHelloProfileMapping` in Curl.Networking does). ADR-0481 (SRP exponentiation) becomes superseded by ADR-0480.

## Acceptance criteria

- [ ] `git grep -n 'Srp' -- Curl.Tls.UnitLibrary` finds nothing but `TlsExtensionType.Srp` if it is kept.
- [ ] `dotnet build` and the fast tests pass, and Curl.Tls.UnitLibrary stays at 100% line and branch coverage.

## Notes

## Log

- 2026-10-13: Created.
