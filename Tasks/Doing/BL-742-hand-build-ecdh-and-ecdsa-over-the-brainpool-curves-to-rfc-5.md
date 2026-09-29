---
id: BL-742
title: Hand-build ECDH and ECDSA over the brainpool curves to RFC 5639
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-742 — Hand-build ECDH and ECDSA over the brainpool curves to RFC 5639

## Goal

`Curl.Cryptography.UnitLibrary` computes ECDH shared secrets and ECDSA signatures and verifications over `brainpoolP256r1`, `brainpoolP384r1` and `brainpoolP512r1` (RFC 5639) with constant-time scalar multiplication, identically on every platform.

## Context

- ADR-0118 (BL-669): the BCL supports brainpool named curves on Windows 10+, only on some Linux distributions, and not on macOS (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28), so they are hand-built and used everywhere. Types: `BrainpoolEcdh` and `BrainpoolEcdsa`, each taking the curve as a parameter.
- Consumers: TLS 1.3 groups `brainpoolP256r1tls13`, `brainpoolP384r1tls13`, `brainpoolP512r1tls13` and the matching `ecdsa_brainpoolP*r1tls13_sha*` signature schemes (RFC 8734), which OpenSSL lists (`openssl list -tls-groups`, OpenSSL 3.5.7, 2026-09-28), selectable through `--curves` and `--sigalgs` (BL-709); brainpool server certificates (BL-699, BL-703).
- Vectors: RFC 7027 appendix A (ECDH test vectors for P256r1, P384r1, P512r1); RFC 8734 appendix A where applicable; ECDSA signing with the deterministic nonce of RFC 6979 so signatures are reproducible, checked against verification of the Wycheproof `ecdsa_brainpoolP*r1_sha*` vectors (valid and invalid).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass RFC 7027 appendix A for the three curves and verify every Wycheproof brainpool ECDSA vector with its expected result.
- [ ] A peer point not on the curve or at infinity is rejected with the typed failure ADR-0118 names; scalar multiplication has no secret-dependent branch or table index, stated in the XML docs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
