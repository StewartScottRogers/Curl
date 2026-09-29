---
id: BL-847
title: Carry --cert, --ciphers, --tls13-ciphers and the OpenSSL ClientHello profile into QUIC handshakes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-728, BL-787]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-847 — Carry --cert, --ciphers, --tls13-ciphers and the OpenSSL ClientHello profile into QUIC handshakes

## Goal

A QUIC handshake started by `QuicDialer` presents the `--cert` client certificate, offers the suites `--ciphers`/`--tls13-ciphers` select as `HandBuiltTlsProvider` does, and sends the OpenSSL build's TLS 1.3 ClientHello on Linux and macOS (the LibreSSL one on Windows), as ADR-0144 section 5 decides.

## Context

- BL-728 (ADR-0180) dials QUIC with `QuicClientSettings.CreateCurlTlsSettings`, curl.se's LibreSSL profile, on every platform, and passes none of `TlsClientOptions`' client-certificate or cipher options into the handshake.
- `HandBuiltTlsProvider` already loads `--cert` (`ClientCertificateLoader.Load`, `ToTlsClientCertificate`) and selects suites (`SelectCipherSuites`, `OpenSslCipherSuites`); reuse them. BL-787 builds the measured ClientHello profiles in `Curl.Tls.UnitLibrary`.
- Code: `Curl.Networking.UnitLibrary/QuicDialer.cs` (`AttemptAsync`), `Curl.Quic.UnitLibrary/QuicClientSettings.cs`.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` show a QUIC handshake against the in-memory server presenting a `--cert` certificate when the server asks for one, offering only the suites `--tls13-ciphers` names in the OpenSSL build, and failing with exit 59 and `HandBuiltTlsProvider`'s message when none can be offered.
- [ ] A test pins the ClientHello a QUIC handshake sends in the OpenSSL build against the OpenSSL profile BL-787 measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary`.

## Notes

Filed by BL-728.

## Log

- 2026-09-28: Created.
