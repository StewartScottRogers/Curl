---
id: BL-744
title: Hand-build ML-DSA to FIPS 204
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-743]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-744 — Hand-build ML-DSA to FIPS 204

## Goal

`Curl.Cryptography.UnitLibrary` generates ML-DSA-44, ML-DSA-65 and ML-DSA-87 key pairs, signs (hedged and deterministic) and verifies (FIPS 204, pure ML-DSA with a context string), reusing the SHAKE functions BL-743 hand-builds.

## Context

- ADR-0118 (BL-669): the BCL's `MLDsa` works only on Linux with OpenSSL 3.5+ and on Windows Insider builds (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28). The type is `MlDsa`, taking the parameter set.
- Consumers: TLS 1.3 signature schemes `mldsa44`, `mldsa65`, `mldsa87` (draft-ietf-tls-mldsa) that OpenSSL 3.5 offers, for server certificates and `CertificateVerify` in the hand-built TLS client (BL-699, BL-709), and ML-DSA client certificates for `--cert`/`--key`.
- Vectors: the NIST ACVP ML-DSA `keyGen`, `sigGen` (deterministic) and `sigVer` vectors (FIPS 204 final).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` reproduce a set of ACVP `keyGen` and deterministic `sigGen` vectors and every `sigVer` vector's expected result chosen, for each parameter set.
- [ ] Secret-dependent work (the rejection loop aside, which FIPS 204 allows to leak its iteration count) has no secret-dependent branch or table index, stated in the XML docs; the private key is zeroed on `Dispose`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
