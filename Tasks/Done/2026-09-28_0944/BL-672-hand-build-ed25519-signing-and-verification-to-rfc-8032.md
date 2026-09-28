---
id: BL-672
title: Hand-build Ed25519 signing and verification to RFC 8032
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-671]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-672 — Hand-build Ed25519 signing and verification to RFC 8032

## Goal

`Curl.Cryptography.UnitLibrary` derives Ed25519 public keys from 32-byte seeds, signs and verifies exactly as RFC 8032 section 5.1 specifies, reusing BL-671's field arithmetic.

## Context

- Consumers: SSH `ssh-ed25519` and `ssh-ed25519-cert-v01@openssh.com` host keys (BL-678, BL-566), Ed25519 user keys (BL-681), TLS 1.3 `ed25519` CertificateVerify and certificate signatures (BL-699). API and rules: BL-669's ADR.
- RFC 8032 section 5.1 (Ed25519: encoding, decoding with the canonical checks, SHA-512 from the BCL), section 7.1 (test vectors TEST 1, 2, 3, 1024 and SHA(abc)). Verification rejects a non-canonical `S` (S >= L) and a point that fails to decode.
- Signing is constant-time in the secret scalar; verification need not be.

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` reproduce every RFC 8032 section 7.1 Ed25519 vector (public key from secret, signature bytes, verification true), and verification is false for a flipped message bit, a flipped signature bit, `S >= L` and an undecodable public key.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Shape: public `Ed25519` (`GeneratePrivateKey`, `ComputePublicKey`, `Sign`, `Verify`;
  sizes 32/32/64), internal `Edwards25519` (extended-coordinate points, complete
  addition, 256-bit masked-swap ladder, encode, RFC 8032 5.1.3 decode) and internal
  `Scalar25519` (mod-L reduction after TweetNaCl's `modL`, `MultiplyAdd`,
  `IsBelowOrder`). `Field25519` gained `Negate`, `PowerByPublicExponent`, `AreEqual`
  and `Parity`. The constants d, 2d, sqrt(-1) and B are computed at type
  initialisation from small integers and B's encoding rather than typed in as limbs,
  so a mistyped constant cannot slip in.
- Verification is cofactorless: encode([S]B + [k](-A)) is compared with R. RFC 8032
  section 5.1.7 permits it ("sufficient, but not required"), and it is what OpenSSL's
  `ED25519_verify` does, so Curl accepts and rejects the same signatures as the
  OpenSSL-backed curl builds. Within ADR-0118's scope, so recorded here and in the
  type's XML remarks rather than a new ADR.
- `Sign` derives the public key itself (one extra scalar multiplication) so the API
  takes just the seed; a caller with a cached public key can get an overload later if
  profiling asks for one.
- Test vectors were taken from the RFC text at rfc-editor.org (2026-09-28), including
  TEST 1024's 1023-byte message; SHA(abc)'s message is computed with `SHA512.HashData`.
  The S >= L cases use S + L (same point, so only the range check rejects it) and L.
- Pipeline stages run inline in this lane session: the change is one library and its
  tests, with the RFC as the plan.
- Results: 178 Cryptography tests pass; `Measure-CodeQuality.ps1 -Library
  Curl.Cryptography.UnitLibrary` gives 100% line, 100% branch, 92 members, 0 failing,
  worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Ed25519 derives public keys, signs and verifies to RFC 8032 section 5.1, reproducing every section 7.1 vector
