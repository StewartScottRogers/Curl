---
id: BL-818
title: Offer TLS 1.3 and TLS 1.2 in one hand-built ClientHello and continue on the version the ServerHello picks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-818 — Offer TLS 1.3 and TLS 1.2 in one hand-built ClientHello and continue on the version the ServerHello picks

## Goal

A hand-built TLS connection whose range spans TLS 1.3 and TLS 1.2 (or lower) sends one ClientHello offering every version in the range, and completes the handshake as TLS 1.3 or as TLS 1.2 by what the ServerHello selects, the `TlsClientConnection` ADR-0140's class structure names; `HandBuiltTlsProvider` uses it in place of choosing `Tls13ClientConnection` or `Tls12ClientConnection` from the range alone.

## Context

- ADR-0160 decision 3 (BL-708): today a range reaching TLS 1.3 runs `Tls13ClientConnection` only, so a hand-built route with `--tlsv1.2` and no ceiling offers only TLS 1.3 and cannot talk to a TLS 1.2 server. No routing row sends such a range there yet, but BL-618's rows (`--curves`, `--sigalgs` and the rest) will.
- ADR-0140, "Class structure": `TlsClientConnection.ConnectAsync(Stream, TlsClientSettings, IServerCertificateVerifier, CancellationToken)` sends the hello and picks the TLS 1.3 or 1.2 path from the ServerHello. RFC 8446 section 4.1.3's downgrade sentinels must be checked when TLS 1.2 is chosen with TLS 1.3 offered.
- Code: `Curl.Tls.UnitLibrary/Tls13ClientConnection.cs`, `Tls12ClientConnection.cs`, `Tls13ClientHandshake.cs`, `Tls12ClientHandshake.cs`; `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs` (`HandshakeAsync`, `OffersTls13`).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` complete a handshake with one ClientHello offering TLS 1.3 and TLS 1.2 against `Tls13RecordTestServer` (continuing as TLS 1.3) and against `Tls12RecordTestServer` (continuing as TLS 1.2), and a TLS 1.2 ServerHello carrying the TLS 1.3 downgrade sentinel fails with `illegal_parameter`.
- [ ] `Curl.Networking.UnitTests` show `HandBuiltTlsProvider` with `MinimumVersion` TLS 1.2 and no ceiling completing against a TLS 1.2-only server-side `SslStream`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Tls.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Filed by BL-708 (ADR-0160).

## Log

- 2026-09-28: Created.
