---
id: BL-802
title: Offer the DHE-DSS suites in the TLS 1.2 client handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-703, BL-745]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-802 — Offer the DHE-DSS suites in the TLS 1.2 client handshake

## Goal

`Tls12ClientHandshake` offers and completes the DHE_DSS suites, verifying the server's DSA-signed ServerKeyExchange with the hand-built DSA of BL-745 at TLS 1.2, 1.1 and 1.0.

## Context

- BL-703 built `Tls12ClientHandshake` in `Curl.Tls.UnitLibrary`: the I/O-free TLS 1.2/1.1/1.0 client handshake over `Tls12CipherSuite`, with `Tls12Authentication` (today `Rsa`, `Ecdsa`, `Anonymous`), `TlsSignatureScheme`, `TlsSignatureKind`, `TlsCertificatePublicKey`, and the in-memory `Tls12TestServer` (credentials in `TestServerCredential`) in `Curl.Tls.UnitTests`. Its decisions are in ADR-0154 (`Documentation/Planning/Decisions/ADR-0154-the-tls-1-2-client-handshake-refuses-legacy-renegotiation-ignores-hello-request-and-matches-openssls-alerts.md`).
- ADR-0140 requires every suite of OpenSSL 3.5.5's `ALL:COMPLEMENTOFALL` list whose key exchange curl can drive (it names DHE-DSS among them) and DSA among the signature algorithms and client certificate keys.
- BL-745 hand-builds DSA (FIPS 186-4) in `Curl.Cryptography.UnitLibrary`. Use it, not `System.Security.Cryptography.DSA`, which is unavailable or limited on macOS.
- Suites to add (DHE_DSS_WITH_…): `0x0013` 3DES_EDE_CBC_SHA, `0x0032` AES_128_CBC_SHA, `0x0038` AES_256_CBC_SHA, `0x0040` AES_128_CBC_SHA256, `0x006a` AES_256_CBC_SHA256, `0x00a2` AES_128_GCM_SHA256, `0x00a3` AES_256_GCM_SHA384, `0x0044` CAMELLIA_128_CBC_SHA, `0x0087` CAMELLIA_256_CBC_SHA, `0x00bd` CAMELLIA_128_CBC_SHA256, `0x00c3` CAMELLIA_256_CBC_SHA256, `0xc056` ARIA_128_GCM_SHA256, `0xc057` ARIA_256_GCM_SHA384. The SHA-256/384 and GCM suites are TLS 1.2 only.
- Signatures: in TLS 1.2 the ServerKeyExchange carries a SignatureAndHashAlgorithm: `0x0202` dsa_sha1, `0x0302` dsa_sha224, `0x0402` dsa_sha256, `0x0502` dsa_sha384, `0x0602` dsa_sha512 (RFC 5246 section 7.4.1.4.1). In TLS 1.0 and 1.1 a DSA ServerKeyExchange is signed over SHA-1 alone, with no algorithm field (RFC 2246 section 7.4.3). The signature is the DER `Dss-Sig-Value` SEQUENCE of r and s.

## Acceptance criteria

- [ ] `Tls12Authentication.Dss` exists and `Tls12CipherSuite` has the 13 DHE_DSS suites above, each pinned by a `Tls12CipherSuiteTests` test.
- [ ] `TlsSignatureScheme` accepts the five dsa_* codes for a DSA certificate key and the legacy DSA-over-SHA-1 rule at TLS 1.0 and 1.1, and `TlsCertificatePublicKey` verifies them with the BL-745 DSA; `Tls12SignatureTests` verifies a known-good signature and rejects a flipped bit.
- [ ] `Tls12TestServer` gains a DSA credential, and `Tls12ClientHandshakeTests` complete an in-memory handshake on a DHE_DSS suite at TLS 1.2 (e.g. `0x00a2`), TLS 1.1 and TLS 1.0 (e.g. `0x0032`), with an application-data record protected by the derived keys decrypted by the other side.
- [ ] A DSA ServerKeyExchange whose signature does not verify fails the handshake with the alert ADR-0154 names for a bad signature (a named test in `Tls12ClientHandshakeFailureTests`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Signing a CertificateVerify with a DSA client key is not part of this task; it is server-side authentication only.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
