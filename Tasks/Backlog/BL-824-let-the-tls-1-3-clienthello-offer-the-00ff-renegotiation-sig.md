---
id: BL-824
title: Let the TLS 1.3 ClientHello offer the 00ff renegotiation signalling value for curl's QUIC profile
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-724]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-824 — Let the TLS 1.3 ClientHello offer the 00ff renegotiation signalling value for curl's QUIC profile

## Goal

`QuicClientSettings.CreateLibreSslTlsSettings` offers cipher suites `1302 1303 1301 00ff`, exactly as ADR-0144 section 5 measured curl.se's ngtcp2 build (LibreSSL 4.2.1) sending, and the pinned first Initial in `Curl.Quic.UnitTests` is re-pinned with it.

## Context

- ADR-0165 decision 3: `Tls13ClientSettings.Validate` refuses any suite that is not TLS 1.3 (`Tls13CipherSuite.Find`), so `TLS_EMPTY_RENEGOTIATION_INFO_SCSV` (`0x00ff`, RFC 5746 section 3.3) cannot be listed and BL-724 left it out.
- Files: `Curl.Tls.UnitLibrary/Tls13ClientSettings.cs` (let `0x00ff` pass validation, or add a separate signalling-values setting), `Tls13ClientHandshake.cs` (a ServerHello choosing `0x00ff` must still be refused with `illegal_parameter`), `Curl.Quic.UnitLibrary/QuicClientSettings.cs`, `Curl.Quic.UnitTests/QuicClientHandshakeTests.cs` (`Start_FixedRandomness_SendsThePinned1200ByteInitial`, `Start_CurlSettings_SendsCurlsClientHelloInOneCryptoFrameThenPadding`).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` show a ClientHello offering `00ff` after the TLS 1.3 suites, and a ServerHello that selects `00ff` failing with `IllegalParameter`.
- [ ] `Curl.Quic.UnitTests` pin the first Initial's cipher suites as `1302 1303 1301 00ff` and its new SHA-256.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` and `-Library Curl.Quic.UnitLibrary` report 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
