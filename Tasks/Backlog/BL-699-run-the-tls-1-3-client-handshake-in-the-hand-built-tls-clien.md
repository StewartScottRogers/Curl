---
id: BL-699
title: Run the TLS 1.3 client handshake in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-697, BL-698, BL-671, BL-672, BL-673]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-699 — Run the TLS 1.3 client handshake in the hand-built TLS client

## Goal

The hand-built client runs a full TLS 1.3 client handshake as a message-level state machine (handshake bytes in and out per encryption level, so QUIC can carry them): key shares on X25519 and the NIST curves, HelloRetryRequest, the `TLS_AES_128_GCM_SHA256`, `TLS_AES_256_GCM_SHA384` and `TLS_CHACHA20_POLY1305_SHA256` suites, ALPN, SNI, CertificateVerify with RSA-PSS, ECDSA and Ed25519, the server Finished check, an optional client certificate, and the server chain handed to the verifier BL-695's ADR names.

## Context

- Design: BL-695's ADR. Builds on BL-697 (key schedule), BL-698 (messages); primitives X25519 (BL-671), Ed25519 (BL-672), ChaCha20-Poly1305 (BL-673) from `Curl.Cryptography.UnitLibrary` (add the reference here), the rest from the BCL.
- Reference: RFC 8448 section 3 (the full 1-RTT trace with its fixed ephemeral keys and randoms, which the tests inject) and section 5 (HelloRetryRequest). Alerts per RFC 8446 section 6 map to typed failures that the TCP and QUIC users turn into curl's exits (35 for a handshake failure, 60 for verification, as the existing `SslStreamTlsProvider` does).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` replay RFC 8448 section 3 as the client (injected keys and random), producing the trace's client flight byte for byte and accepting the server's, and complete a HelloRetryRequest exchange per section 5.
- [ ] An in-memory TLS 1.3 server in the tests (built from the same pieces with a generated certificate) completes handshakes for every suite and key-share group and for each signature scheme, and a bad Finished, a bad CertificateVerify, an unsupported group and a downgrade sentinel each fail with the typed alert.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
