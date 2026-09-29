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
completed: 2026-09-29
---
# BL-744 — Hand-build ML-DSA to FIPS 204

## Goal

`Curl.Cryptography.UnitLibrary` generates ML-DSA-44, ML-DSA-65 and ML-DSA-87 key pairs, signs (hedged and deterministic) and verifies (FIPS 204, pure ML-DSA with a context string), reusing the SHAKE functions BL-743 hand-builds.

## Context

- ADR-0118 (BL-669): the BCL's `MLDsa` works only on Linux with OpenSSL 3.5+ and on Windows Insider builds (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28). The type is `MlDsa`, taking the parameter set.
- Consumers: TLS 1.3 signature schemes `mldsa44`, `mldsa65`, `mldsa87` (draft-ietf-tls-mldsa) that OpenSSL 3.5 offers, for server certificates and `CertificateVerify` in the hand-built TLS client (BL-699, BL-709), and ML-DSA client certificates for `--cert`/`--key`.
- Vectors: the NIST ACVP ML-DSA `keyGen`, `sigGen` (deterministic) and `sigVer` vectors (FIPS 204 final).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` reproduce a set of ACVP `keyGen` and deterministic `sigGen` vectors and every `sigVer` vector's expected result chosen, for each parameter set.
- [x] Secret-dependent work (the rejection loop aside, which FIPS 204 allows to leak its iteration count) has no secret-dependent branch or table index, stated in the XML docs; the private key is zeroed on `Dispose`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Vectors: `KnownAnswers/ml-dsa-acvp-fips204-selected.txt`, copied unchanged from the ACVP
  server's ML-DSA-keyGen/sigGen/sigVer-FIPS204 `internalProjection.json` (fetched
  2026-09-29): keyGen 1, 2, 26, 27, 51, 52; deterministic sigGen 2, 7, 35, 41, 71, 72 and
  hedged sigGen 194, 214, 251 (external interface, pure, with context); sigVer 15 cases,
  for each set one valid and one each of modified message, commitment, z and hint. All
  pass, with 5 more negative and round-trip tests and the hint-encoding tests.
- Types: `MlDsaParameterSet`, `MlDsaParameters`, `MlDsaPolynomial`, `MlDsaEncoding`,
  `MlDsaSampling`, `MlDsaInternalFunctions`, `MlDsa`, following `MlKem`'s shape
  (ADR-0118's API rules). Method names `SignData`, `SignDataDeterministic` and
  `VerifyData` follow the BCL's `MLDsa`.
- Defaults taken (decided under the delegation, within ADR-0118's rules, so no new ADR):
  only pure ML-DSA (HashML-DSA and external mu are not in the goal; no consumer needs them
  yet); a context over 255 bytes is `ArgumentException` for signing and verifying, as
  `Ed448` does; `ImportPrivateKey` recomputes the public key and throws when the key's tr
  or t0 does not match, the ML-DSA counterpart of `MlKem`'s H(ek) check.
- Constant time: reduction mod q uses the high half of a 64-bit product with
  floor(2^64 / q) (exact to within one for every 64-bit value, then a masked subtract).
  Besides the rejection loop, the secret-seeded samplers (ExpandS, SampleInBall) branch
  on whether a hash byte is rejected, as FIPS 204 specifies them; that reveals only
  rejected values. SampleInBall places coefficients with a masked pass, never by index.
- Coverage was measured over `Curl.Cryptography.UnitTests` alone (`dotnet test --collect`
  into a separate results directory, then `Measure-CodeQuality.ps1 -SkipTestRun
  -ResultsDirectory`), because one whole-solution run hit an unrelated
  `Curl.Conformance` timeout under load; the solution-wide fast run afterwards was green.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ML-DSA-44/65/87 key generation, hedged and deterministic signing and verification (FIPS 204 pure, with context) match NIST ACVP vectors
