---
id: BL-1202
title: Report the hand-built TLS client's OpenSSL-build handshake -v lines before its exit 60 and exit 35
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1178]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1202 — Report the hand-built TLS client's OpenSSL-build handshake -v lines before its exit 60 and exit 35

## Goal

`HandBuiltTlsProvider`'s OpenSSL build (`--cert-status`, `--curves`, `--ech` and the other ADR-0140 routes) prints the handshake `-v` lines curl 8.18.0 prints before an exit 60 (untrusted certificate, host name mismatch) and only the ALPN offer before an exit 35, as `SslStreamTlsProvider` does since BL-1178.

## Context

- Follow-up from BL-1178 (ADR-0371), which made the hand-built OpenSSL build report a failed `TlsHandshakeEvent` on a pin refusal only.
- `HandBuiltTlsProvider.ReportFailedHandshake` returns early in the OpenSSL build unless `PinnedPublicKeyRefused`; `HandBuiltHandshake.NegotiatedBy(verifier.Presented)` already gives the version, suite and ALPN protocol, and a null `Presented` means nothing was negotiated.
- The measured lines are in ADR-0371's Context; `OpenSslHandshakeText` already words both cases.

## Acceptance criteria

- [ ] A `HandBuiltTlsProviderTests` test with the OpenSSL build flag pins a failed handshake event carrying the certificate before an untrusted-certificate exit 60.
- [ ] A test pins a failed event with `SslProtocols.None` and the ALPN offer before a hand-built exit 35.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for Curl.Networking.UnitLibrary.

## Notes

## Log

- 2026-10-02: Created.
