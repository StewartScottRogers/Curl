---
id: BL-743
title: Hand-build ML-KEM to FIPS 203
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-743 — Hand-build ML-KEM to FIPS 203

## Goal

`Curl.Cryptography.UnitLibrary` generates ML-KEM-512, ML-KEM-768 and ML-KEM-1024 key pairs, encapsulates and decapsulates (FIPS 203, with implicit rejection) in constant time, and exposes the SHA-3 family it needs as public types of its own (`Sha3` for SHA3-256 and SHA3-512, `Shake` for SHAKE128 and SHAKE256, FIPS 202), because the BCL's SHA-3 family is not supported on macOS; Ed448 (BL-741) and ML-DSA (BL-744) reuse them.

## Context

- ADR-0118 (BL-669): the BCL's `MLKem` works only on Linux with OpenSSL 3.5+ and on Windows Insider builds, and SHA-3 is missing on macOS (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28). The type is `MlKem`, taking the parameter set.
- Consumers: the TLS 1.3 hybrid groups `X25519MLKEM768` (OpenSSL 3.5's first default key share), `SecP256r1MLKEM768` and `SecP384r1MLKEM1024` and the pure `MLKEM512/768/1024` groups (draft-ietf-tls-ecdhe-mlkem, draft-ietf-tls-mlkem), listed by `openssl list -tls-groups` (OpenSSL 3.5.7, 2026-09-28), in the hand-built TLS client (BL-699, BL-709).
- Vectors: the NIST ACVP ML-KEM `keyGen` and `encapDecap` known-answer vectors (FIPS 203 final), including the decapsulation implicit-rejection cases; FIPS 202 vectors for the internal SHA-3 and SHAKE functions.

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` reproduce a set of ACVP `keyGen`, `encapsulation` and `decapsulation` vectors for each parameter set, including one modified ciphertext that yields the implicit-rejection key.
- [x] `Curl.Cryptography.UnitTests` pass the FIPS 202 example values (NIST "SHA-3 Examples" and the CAVP byte-oriented SHA3-256, SHA3-512, SHAKE128 and SHAKE256 short-message vectors) for `Sha3` and `Shake`.
- [x] Decapsulation compares the re-encrypted ciphertext without a secret-dependent branch (a masked select), stated in the XML docs; the decapsulation key is zeroed on `Dispose`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered (2026-09-29): `KeccakPermutation`, `KeccakSponge`, public `Sha3` (SHA3-256,
  SHA3-512) and `Shake` (SHAKE128, SHAKE256, one-shot and incremental `AppendData`/`Read`),
  `MlKemParameterSet`, `MlKemParameters`, `MlKemPolynomial`, `MlKemPublicKeyEncryption`
  (K-PKE) and public `MlKem`. 52 new tests; `Measure-CodeQuality.ps1 -Library
  Curl.Cryptography.UnitLibrary`: 100% line, 100% branch, 504 members, 0 failing, worst CRAP 10.
- Vectors: `KnownAnswers/ml-kem-acvp-fips203-selected.txt` holds 30 tests copied unchanged
  from the ACVP server's `ML-KEM-keyGen-FIPS203` and `ML-KEM-encapDecap-FIPS203`
  `internalProjection.json` (two keyGen and one encapsulation per set, one valid and one
  "modified ciphertext" decapsulation per set, and decapsulation-key and encapsulation-key
  checks). The four CAVP byte-oriented ShortMsg files (SHA3-256, SHA3-512, SHAKE128,
  SHAKE256) are embedded whole as `.txt`. The NIST "SHA-3 Examples" 1600-bit 0xA3 values
  were cross-checked on Windows against the BCL's SHA3/SHAKE before pinning.
- Default taken, API shape (ADR-0118's rules applied, no new decision): `MlKem` is an
  `IDisposable` instance holding the decapsulation key, because the key must be zeroed on
  `Dispose`; encapsulation is static because it needs only the peer's key. The parameter
  set is a public enum beside it, as ADR-0118 names "`MlKem`, taking the parameter set".
- Default taken, input checks (FIPS 203 section 7): an encapsulation key failing the
  modulus check comes from a peer, so `TryEncapsulate` returns `false` with its outputs
  zeroed; a decapsulation key failing the hash check is the caller's own key, so
  `ImportDecapsulationKey` throws `ArgumentException` - ADR-0118's caller-mistake rule.
- No new ADR: the choices above follow ADR-0118's existing rules, and BL-886 (in Doing)
  holds `Documentation/Planning/Decisions`.
- Constant time: coefficient reduction divides by multiplying with floor(2^32/q) and a
  masked correction; only `SampleNtt` branches, on the public rho, as FIPS 203 allows.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Curl.Cryptography hand-builds ML-KEM-512/768/1024 (FIPS 203, implicit rejection) with public Sha3 and Shake, pinned to ACVP and CAVP vectors at 100% coverage
