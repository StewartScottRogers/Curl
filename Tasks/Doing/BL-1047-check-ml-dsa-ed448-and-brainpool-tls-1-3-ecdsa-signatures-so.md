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
completed:
---
# BL-1047 — Check ML-DSA, ed448 and brainpool TLS 1.3 ECDSA signatures so --sigalgs can offer them

## Goal

`--sigalgs` lists naming `mldsa44`/`mldsa65`/`mldsa87` (0x0904-0x0906), `ed448` (0x0808) or `ecdsa_brainpoolP256r1tls13_sha256`/`...P384r1tls13_sha384`/`...P512r1tls13_sha512` (0x081a-0x081c) offer them, and a server certificate signed with one verifies, as OpenSSL 3.5's curl does, instead of the schemes being dropped.

## Context

- BL-709 / ADR-0284 decision 3: `OpenSslSignatureAlgorithmList` maps these names, but `ClientHelloProfileMapping.CheckableSignatureAlgorithms` drops them because `TlsSignatureScheme` cannot check them, so `--sigalgs mldsa65` alone fails exit 35 "no suitable signature algorithm" where OpenSSL offers it.
- Hand-build the verifiers in `Curl.Tls.UnitLibrary` (ML-DSA is FIPS 204; ed448 RFC 8032; brainpool ECDSA RFC 8734) and add them to `TlsSignatureScheme`'s rules; the default OpenSSL profile's measured list then needs no cutting either.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` verify each scheme against known-answer vectors and in a TLS 1.3 CertificateVerify.
- [ ] `HandBuiltTlsProviderTests` pin the offered `signature_algorithms` for each name as Ubuntu's curl 8.18.0 sent them (0x0905 for `mldsa65`, and so on).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
