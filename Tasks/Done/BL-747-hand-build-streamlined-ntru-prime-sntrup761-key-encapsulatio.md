---
id: BL-747
title: Hand-build Streamlined NTRU Prime sntrup761 key encapsulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-747 — Hand-build Streamlined NTRU Prime sntrup761 key encapsulation

## Goal

`Curl.Cryptography.UnitLibrary` exposes `Sntrup761` (public type, namespace `Curl.Cryptography`) that generates key pairs, encapsulates and decapsulates exactly as the Streamlined NTRU Prime round-3 submission's `sntrup761` parameter set does (the variant OpenSSH and libssh use for `sntrup761x25519-sha512`), in constant time, with every random input also accepted as a parameter.

## Context

- ADR-0122 (BL-560) found `sntrup761x25519-sha512` and `sntrup761x25519-sha512@openssh.com` in libssh 0.12.2's key-exchange list (measured 2026-09-28) and amends ADR-0118's hand-built list with this primitive; the BCL has no NTRU Prime on any platform.
- Specification: "NTRU Prime: round 3" (2020-10-07), sntrup761 (p=761, q=4591, w=286); OpenSSH's `sntrup761.c` is the reference the SSH wire format follows (public key 1158 bytes, ciphertext 1039 bytes, shared secret 32 bytes, hash SHA-512 truncated to 32 bytes).
- Follow ADR-0118's API shape, constant-time and zeroing rules; SHA-512 comes from the BCL.
- Consumer: BL-748.

## Acceptance criteria

- [x] `Sntrup761.GenerateKeyPair`, `Encapsulate` and `Decapsulate` exist with span-based signatures and overloads that take the random bytes, per ADR-0118.
- [x] Tests reproduce the round-3 submission's KAT (`kat_kem.rsp` for sntrup761, first vectors) byte for byte, citing the source beside each vector.
- [x] A tampered ciphertext decapsulates to the implicit-rejection secret, not an exception, and a test pins it.
- [x] Coverage 100% line and branch for the new type, no method over complexity 10, `dotnet build` clean and fast tests green.

## Notes

- Ported from the round-3 submission's `Reference_Implementation/kem/sntrup761` (`ntruprime-20201007.tar.gz`, downloaded 2026-09-28): `Uint14Division` (constant-time divmod), `SortingNetwork` (djbsort), `Sntrup761Ring` (R/3 and R/q arithmetic, reciprocals, Decrypt), `Sntrup761Encoding` (Encode/Decode, Rq, Rounded, Small) and the public `Sntrup761`.
- Choice (sensible default): the random-bytes key generation is `TryGenerateKeyPair(random, publicKey, secretKey)`, not a `GenerateKeyPair` overload, because the reference redraws g when it has no inverse mod 3, so fixed random bytes can be rejected; it returns `false` with both keys zeroed, and `GenerateKeyPair(publicKey, secretKey)` redraws until it succeeds. The random layout is the order the reference draws: 4p bytes for g, 4p for f, 191 for rho (`KeyGenerationRandomSize` 6279); encapsulation takes 4p (`EncapsulationRandomSize` 3044). Within ADR-0118's API shape, so no new ADR.
- Choice: products are accumulated unreduced in `int` and frozen once per coefficient instead of after every term; the results are the same residues, and the KAT pins it.
- KAT: the first three vectors of `KAT/kem/sntrup761/kat_kem.rsp` are embedded unchanged as `KnownAnswers/sntrup761-kat_kem-first-three.txt`; the test replays NIST's AES-256 CTR_DRBG (`NistKnownAnswerRandom`, from `nist/rng.c`) in the reference's call pieces (four bytes per `urandom32`, one 191-byte call for rho). All three match byte for byte.
- Coverage of the five new types: 100% line, 100% branch (cobertura from the fast test run). Build clean with CA1502 at 10.
- Ported from the round-3 submission's `Reference_Implementation/kem/sntrup761` (`ntruprime-20201007.tar.gz`, downloaded 2026-09-28): `Uint14Division` (constant-time divmod), `SortingNetwork` (djbsort), `Sntrup761Ring` (R/3 and R/q arithmetic, reciprocals, Decrypt), `Sntrup761Encoding` (Encode/Decode, Rq, Rounded, Small) and the public `Sntrup761`.
- Choice (sensible default): the random-bytes key generation is `TryGenerateKeyPair(random, publicKey, secretKey)`, not a `GenerateKeyPair` overload, because the reference redraws g when it has no inverse mod 3, so fixed random bytes can be rejected; it returns `false` with both keys zeroed, and `GenerateKeyPair(publicKey, secretKey)` redraws until it succeeds. The random layout is the order the reference draws: 4p bytes for g, 4p for f, 191 for rho (`KeyGenerationRandomSize` 6279); encapsulation takes 4p (`EncapsulationRandomSize` 3044). Within ADR-0118's API shape, so no new ADR.
- Choice: products are accumulated unreduced in `int` and frozen once per coefficient instead of after every term; the results are the same residues, and the KAT pins it.
- KAT: the first three vectors of `KAT/kem/sntrup761/kat_kem.rsp` are embedded unchanged as `KnownAnswers/sntrup761-kat_kem-first-three.txt`; the test replays NIST's AES-256 CTR_DRBG (`NistKnownAnswerRandom`, from `nist/rng.c`) in the reference's call pieces (four bytes per `urandom32`, one 191-byte call for rho). All three match byte for byte.
- Coverage of the five new types: 100% line, 100% branch (cobertura from the fast test run). Build clean with CA1502 at 10.
## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Sntrup761 generates, encapsulates and decapsulates sntrup761 byte for byte the round-3 KAT, with implicit rejection
