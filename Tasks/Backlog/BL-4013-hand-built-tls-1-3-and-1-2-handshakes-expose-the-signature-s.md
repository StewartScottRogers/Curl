---
id: BL-4013
title: Hand-built TLS 1.3 and 1.2 handshakes expose the signature scheme the server signed with
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4012]
touches: [Curl.Tls.UnitLibrary/Tls13ClientHandshake.cs, Curl.Tls.UnitLibrary/Tls12ClientHandshake.cs, Curl.Tls.UnitTests/PeerSignatureSchemeTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4013 — Hand-built TLS 1.3 and 1.2 handshakes expose the signature scheme the server signed with

## Goal

`Tls13ClientHandshake` and `Tls12ClientHandshake` expose the `TlsSignatureScheme` code the server signed with (TLS 1.3 CertificateVerify; TLS 1.2 ServerKeyExchange, or none for RSA key exchange), and the public key type behind it, so the caller can name it as OpenSSL's `OBJ_nid2sn(SSL_get_peer_signature_type_nid)` does.

## Context

Split out of BL-4009, which sets `TlsHandshakeEvent.PeerSignatureTypeName` (`RSASSA-PSS`, `rsaEncryption`, `id-ecPublicKey`, `ED25519`) on the hand-built path. `NegotiatedGroup` is already exposed on both handshakes; the signature scheme is not. Depends on BL-4012 only because both edit the two handshake files.

## Acceptance criteria

- [ ] A unit test pins the peer signature scheme after a TLS 1.3 handshake (RSA-PSS and ECDSA certificates) and after a TLS 1.2 ECDHE handshake.
- [ ] TLS 1.2 RSA key exchange (no ServerKeyExchange) leaves it unset, pinned by a test.
- [ ] Curl.Tls.UnitLibrary stays at 100% line and branch coverage, complexity at most 10.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-28: Created.
