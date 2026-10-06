---
id: BL-742
title: Hand-build ECDH and ECDSA over the brainpool curves to RFC 5639
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Documentation/Planning/Decisions/ADR-0217-brainpool-ecdh-and-ecdsa-use-complete-addition-formulas-uncompressed-points-and-p1363-signatures.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-742 — Hand-build ECDH and ECDSA over the brainpool curves to RFC 5639

## Goal

`Curl.Cryptography.UnitLibrary` computes ECDH shared secrets and ECDSA signatures and verifications over `brainpoolP256r1`, `brainpoolP384r1` and `brainpoolP512r1` (RFC 5639) with constant-time scalar multiplication, identically on every platform.

## Context

- ADR-0118 (BL-669): the BCL supports brainpool named curves on Windows 10+, only on some Linux distributions, and not on macOS (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28), so they are hand-built and used everywhere. Types: `BrainpoolEcdh` and `BrainpoolEcdsa`, each taking the curve as a parameter.
- Consumers: TLS 1.3 groups `brainpoolP256r1tls13`, `brainpoolP384r1tls13`, `brainpoolP512r1tls13` and the matching `ecdsa_brainpoolP*r1tls13_sha*` signature schemes (RFC 8734), which OpenSSL lists (`openssl list -tls-groups`, OpenSSL 3.5.7, 2026-09-28), selectable through `--curves` and `--sigalgs` (BL-709); brainpool server certificates (BL-699, BL-703).
- Vectors: RFC 7027 appendix A (ECDH test vectors for P256r1, P384r1, P512r1); RFC 8734 appendix A where applicable; ECDSA signing with the deterministic nonce of RFC 6979 so signatures are reproducible, checked against verification of the Wycheproof `ecdsa_brainpoolP*r1_sha*` vectors (valid and invalid).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass RFC 7027 appendix A for the three curves and verify every Wycheproof brainpool ECDSA vector with its expected result.
- [x] A peer point not on the curve or at infinity is rejected with the typed failure ADR-0118 names; scalar multiplication has no secret-dependent branch or table index, stated in the XML docs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built: `BrainpoolCurve` (public enum), `BrainpoolDomainParameters` and `BrainpoolPoint` (internal), `BrainpoolEcdh` (public static) and `BrainpoolEcdsa` (public, `IDisposable`). `MontgomeryModulus` gains `ToMontgomeryForm`. Design recorded in ADR-0217 (Decided by Claude under Stewart's delegation): Renes-Costello-Batina complete addition (brainpool r1 curves have cofactor 1), a fixed 4-bit-window ladder that reads the whole table, Fermat inversion, uncompressed points only, r || s signatures with RFC 6979 nonces through `DeterministicDsaNonce`, and key generation by reducing 8 extra random bytes mod q.
- Touches widened to the new ADR-0217 file and `Documentation/Planning/Decisions/README.md`, where every ADR is recorded. No task in Doing names either one.
- Wycheproof: "every brainpool ECDSA vector" means the three IEEE P1363 files for the RFC 8734 TLS schemes (P256r1/SHA-256: 261, P384r1/SHA-384: 292, P512r1/SHA-512: 337), converted to `KnownAnswers/wycheproof-ecdsa-*-p1363.txt`. All 890 give their expected result. The DER files exercise a DER parser this API does not have. The P224r1, P320r1 and SHA-3 files cover combinations TLS never uses. The P256r1 file runs in the fast tests (about 2 s in Debug). P384r1 and P512r1 take 7 s and 19 s, so they are `Integration` under ADR-0118's one-second rule; I ran them and they pass.
- Cross-checked on Windows only (`OSCondition`): the shared secret matches CNG's `DeriveRawSecretAgreement` on brainpoolP384r1, and CNG's ECDsa verifies our signatures on all three curves.
- Measure-CodeQuality: Curl.Cryptography.UnitLibrary at 100% line and 100% branch, 618 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ECDH and ECDSA over brainpoolP256r1/P384r1/P512r1 pass RFC 7027 and all 890 Wycheproof P1363 vectors
