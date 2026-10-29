---
id: BL-4013
title: Hand-built TLS 1.3 and 1.2 handshakes expose the signature scheme the server signed with
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4014]
touches: [Curl.Tls.UnitLibrary/Tls13ClientHandshake.cs, Curl.Tls.UnitLibrary/Tls12ClientHandshake.cs, Curl.Tls.UnitTests/PeerSignatureSchemeTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4013 — Hand-built TLS 1.3 and 1.2 handshakes expose the signature scheme the server signed with

## Goal

`Tls13ClientHandshake` and `Tls12ClientHandshake` expose the `TlsSignatureScheme` code the server signed with (TLS 1.3 CertificateVerify; TLS 1.2 ServerKeyExchange, or none for RSA key exchange), and the public key type behind it, so the caller can name it as OpenSSL's `OBJ_nid2sn(SSL_get_peer_signature_type_nid)` does.

## Context

Split out of BL-4009, which sets `TlsHandshakeEvent.PeerSignatureTypeName` (`RSASSA-PSS`, `rsaEncryption`, `id-ecPublicKey`, `ED25519`) on the hand-built path. `NegotiatedGroup` is already exposed on both handshakes; the signature scheme is not. Depends on BL-4014 only because both edit the two handshake files.

## Acceptance criteria

- [x] A unit test pins the peer signature scheme after a TLS 1.3 handshake (RSA-PSS and ECDSA certificates) and after a TLS 1.2 ECDHE handshake.
- [x] TLS 1.2 RSA key exchange (no ServerKeyExchange) leaves it unset, pinned by a test.
- [x] Curl.Tls.UnitLibrary stays at 100% line and branch coverage, complexity at most 10.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Both handshakes now expose PeerSignatureScheme (ushort?) and PeerSignatureKeyAlgorithmOid (string?), set only once the server's signature has verified (TLS 1.3 CertificateVerify, TLS 1.2 ServerKeyExchange). Default taken: TLS 1.0 and 1.1 ServerKeyExchange signatures name no scheme, so the scheme stays null there while the key OID is still set; the caller (BL-4009) maps the key OID to OpenSSL's name (rsaEncryption, id-ecPublicKey, ED25519) and the RSA-PSS scheme to RSASSA-PSS.
- Tests: Curl.Tls.UnitTests/PeerSignatureSchemeTests.cs (TLS 1.3 RSA-PSS and ECDSA, TLS 1.2 ECDHE, TLS 1.2 RSA key exchange unset). Measure-CodeQuality: Curl.Tls.UnitLibrary 100% line, 100% branch, 0 failing members. Fast tests green on Windows; the change is platform-neutral (no paths, OS errors or platform certificates), which covers CI on Linux and macOS.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6ef0dae1 8a05c78a a221f870 3d836d5e.
- 2026-10-28: Doing -> Done. Both hand-built handshakes expose PeerSignatureScheme and PeerSignatureKeyAlgorithmOid once the server's signature verifies
