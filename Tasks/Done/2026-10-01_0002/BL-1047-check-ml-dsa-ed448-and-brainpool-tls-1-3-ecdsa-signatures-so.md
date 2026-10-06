---
id: BL-1047
title: Check ML-DSA, ed448 and brainpool TLS 1.3 ECDSA signatures so --sigalgs can offer them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1047 — Check ML-DSA, ed448 and brainpool TLS 1.3 ECDSA signatures so --sigalgs can offer them

## Goal

`--sigalgs` lists naming `mldsa44`/`mldsa65`/`mldsa87` (0x0904-0x0906), `ed448` (0x0808) or `ecdsa_brainpoolP256r1tls13_sha256`/`...P384r1tls13_sha384`/`...P512r1tls13_sha512` (0x081a-0x081c) offer them, and a server certificate signed with one verifies, as OpenSSL 3.5's curl does, instead of the schemes being dropped.

## Context

- BL-709 / ADR-0284 decision 3: `OpenSslSignatureAlgorithmList` maps these names, but `ClientHelloProfileMapping.CheckableSignatureAlgorithms` drops them because `TlsSignatureScheme` cannot check them, so `--sigalgs mldsa65` alone fails exit 35 "no suitable signature algorithm" where OpenSSL offers it.
- Hand-build the verifiers in `Curl.Tls.UnitLibrary` (ML-DSA is FIPS 204; ed448 RFC 8032; brainpool ECDSA RFC 8734) and add them to `TlsSignatureScheme`'s rules; the default OpenSSL profile's measured list then needs no cutting either.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` verify each scheme against known-answer vectors and in a TLS 1.3 CertificateVerify.
- [x] `HandBuiltTlsProviderTests` pin the offered `signature_algorithms` for each name as Ubuntu's curl 8.18.0 sent them (0x0905 for `mldsa65`, and so on).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- The verifiers were already in place when this ran: BL-940 (commit 95b7a001) added ML-DSA,
  Ed448 and the brainpool TLS 1.3 schemes to `TlsSignatureScheme`'s rules, so
  `CheckableSignatureAlgorithms` already kept them and `--sigalgs mldsa65` alone already
  offered 0x0905. No production code changed; this task added the pins the criteria ask for.
- Known answers: `Curl.Tls.UnitTests/KnownAnswers/openssl-tls13-certificate-verify.txt` holds one
  TLS 1.3 server CertificateVerify per scheme signed by OpenSSL 3.5.5 in WSL Ubuntu (fresh key,
  self-signed certificate, signature over the RFC 8446 content). Chosen over the Curl.Cryptography
  ACVP vectors because those all carry a non-empty context, which TLS never uses, and because a
  signature from another implementation through `TlsCertificatePublicKey` checks the whole TLS
  path. `OpenSslCertificateVerifyKnownAnswerTests` verifies all seven and rejects a changed
  transcript; the handshake tests from BL-940 (`Tls13ClientHandshakeTests.HandshakeCompletesWith*`)
  cover each scheme in a live CertificateVerify.
- Measured 2026-10-01 with Ubuntu's curl 8.18.0 / OpenSSL 3.5.5 against `nc -l`: `--sigalgs <name>`
  sends `signature_algorithms` of exactly its code point (mldsa44 0904, mldsa65 0905, mldsa87 0906,
  ed448 0808, brainpool 081a/081b/081c) and `ed25519:mldsa65` sends 0807 0905. Pinned as
  `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithSigalgs_OffersTheMeasuredSchemesAndTheProfilesGroups` rows.
- Coverage: no library changed, so `Measure-CodeQuality.ps1` has nothing new to measure for this task.
- ADR-0284 decision 3 still says these schemes are dropped; filed BL-1080 to correct it (outside `touches`).

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --sigalgs mldsa44/65/87, ed448 and the brainpool tls13 schemes offer their measured code points, and OpenSSL-signed CertificateVerify vectors verify
