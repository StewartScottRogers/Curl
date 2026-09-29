---
id: BL-917
title: Check ML-DSA, Ed448, brainpool TLS 1.3 and SHA-224 signatures in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-917 — Check ML-DSA, Ed448, brainpool TLS 1.3 and SHA-224 signatures in the hand-built TLS client

## Goal

`TlsSignatureScheme` checks the nine signature schemes the OpenSSL ClientHello profile offers that the client cannot check yet, so the hand-built client's hello carries OpenSSL's whole `signature_algorithms` list (ADR-0222 decision 1).

## Context

- ADR-0222 (BL-820): `HandBuiltTlsProvider` offers only the profile schemes `TlsSignatureScheme.IsCertificateVerifyScheme` or `IsTls12Scheme` accepts, so a server never picks one the client cannot check. The OpenSSL profile (`ClientHelloProfile.OpenSsl`) loses `mldsa65` `0x0905`, `mldsa87` `0x0906`, `mldsa44` `0x0904`, `ed448` `0x0808`, `ecdsa_brainpoolP256r1tls13_sha256` `0x081a`, `ecdsa_brainpoolP384r1tls13_sha384` `0x081b`, `ecdsa_brainpoolP512r1tls13_sha512` `0x081c`, `ecdsa_sha224` `0x0303` and `rsa_pkcs1_sha224` `0x0301`.
- ML-DSA (FIPS 204) and Ed448 (RFC 8032) are not in every BCL build: hand-build what the BCL lacks in its own `Curl.<Area>.UnitLibrary` (root `CLAUDE.md`), which may split this task. Brainpool ECDSA is already verified by hand for TLS 1.2 (ADR-0219).
- Once they are accepted, `ClientHelloProfileMapping.CheckableSignatureAlgorithms` passes them with no change in `Curl.Networking`; its test `CheckableSignatureAlgorithms_LeaveOutOnlyTheOpenSslSchemesTheClientCannotCheck` then needs its expected list emptied.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` verify a TLS 1.3 CertificateVerify (and, for the TLS 1.2 schemes, a ServerKeyExchange) signed with each of the nine schemes, and refuse a bad signature for each.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- Filed by BL-820 (ADR-0222).

## Log

- 2026-09-29: Created.
