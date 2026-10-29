---
id: BL-4208
title: Add a DNS-over-QUIC resolver to Curl.Extension.EncryptedDns
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4207]
touches: [Curl.Extension.EncryptedDns.UnitLibrary/Curl.Extension.EncryptedDns.UnitLibrary.csproj, Curl.Extension.EncryptedDns.UnitLibrary/CLAUDE.md, Curl.Extension.EncryptedDns.UnitLibrary/DnsOverQuic, Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic]
requirement: none
created: 2026-10-29
completed:
---
# BL-4208 — Add a DNS-over-QUIC resolver to Curl.Extension.EncryptedDns

## Goal

`Curl.Extension.EncryptedDns.UnitLibrary` holds an `IDnsResolver` that looks host names up over DNS-over-QUIC (RFC 9250), tested with fake datagram channels.

## Context

Split from BL-4148; builds on BL-4207's codec. Use the hand-built QUIC client in `Curl.Quic.UnitLibrary` (a shared layer, allowed by `VerifyExtensionIsolation`): `QuicClientConnectionState` with `QuicClientSettings` whose `Tls.ApplicationProtocols` is `["doq"]` (RFC 9250 section 4.1.1), `QuicClientConnector.RunHandshakeAsync` over an `IDatagramChannel` from an injected `IDatagramConnector`, then `QuicConnection.OpenBidirectionalStreamAsync`. Default port 853 (section 4.1.1); one query per client bidirectional stream with DNS Message ID 0 (section 4.2.1), the two-octet length prefix (section 4.2), the stream's FIN after the query; a non-zero Message ID in the answer or a missing length prefix is DOQ_PROTOCOL_ERROR (section 4.3, 4.5); the connection closes with DOQ_NO_ERROR (section 4.3). Add the `Curl.Quic.UnitLibrary` reference to the library's csproj.

## Acceptance criteria

- [ ] Unit tests check the DoQ resolver against named sections of RFC 9250 (ALPN `doq`, port 853, Message ID 0, length prefix, one stream per query, DOQ_NO_ERROR and DOQ_PROTOCOL_ERROR) with fake channels or recorded bytes, no network.
- [ ] The build's `VerifyExtensionIsolation` passes with the `Curl.Quic.UnitLibrary` reference.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.EncryptedDns.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4148's run, which split that task for size.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 31a70c7c b1942f80 4d42ce69 3d836d5e.
