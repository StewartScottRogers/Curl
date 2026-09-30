---
id: BL-741
title: Hand-build Ed448 signing and verification to RFC 8032
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-740, BL-743]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-741 — Hand-build Ed448 signing and verification to RFC 8032

## Goal

`Curl.Cryptography.UnitLibrary` derives Ed448 public keys, signs and verifies (PureEdDSA Ed448 with an optional context, RFC 8032 section 5.2) in constant time for the secret parts, reusing X448's field type (BL-740) and the hand-built SHAKE256 (BL-743), because the BCL's `Shake256` is not supported on macOS.

## Context

- ADR-0118 (BL-669): the BCL has no Ed448 on any platform and no SHAKE256 on macOS (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28).
- Consumers: TLS `ed448` signature scheme for server certificates and `CertificateVerify` (BL-699, BL-709), which OpenSSL offers by default; client certificates with Ed448 keys for `--cert`/`--key`.
- Vectors: RFC 8032 section 7.4 (Ed448 test vectors: blank, 1 octet, 1 octet with context, 11, 12, 13, 64, 256 and 1023 octets).
- Verification rejects a non-canonical S or point encoding as RFC 8032 section 5.2.7 requires.

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass every RFC 8032 section 7.4 vector for key derivation, signing and verification.
- [x] Verification returns `false` for a flipped message bit, a flipped signature bit and an S not below the group order.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built as three types mirroring Ed25519's: `Edwards448` (projective X, Y, Z points on
  `Field448`, RFC 8032 section 5.2.4's complete addition used for doubling too, a
  456-bit masked double-and-add ladder, section 5.2.3 decoding), `Scalar448` (scalars
  mod L on the existing `MontgomeryModulus` rather than a new byte-limb reduction, so
  it is constant-time by construction), and the public `Ed448` (`Sign` and `Verify`
  with and without a context; a context over 255 bytes is a caller mistake,
  `ArgumentException`). `Field448` gained `Negate`, `PowerByPublicExponent`, `AreEqual`
  and `Parity`, the same members `Field25519` has.
- Verification is cofactorless ([S]B - [k]A encoded and compared with R's bytes), as
  `Ed25519` already does and RFC 8032 section 5.2.7 permits; a non-canonical R or one
  with any of its last byte's low seven bits set never matches the canonical encoding.
  No ADR: it follows the existing Ed25519 decision under ADR-0118.
- Vectors in `Ed448Tests` were extracted from the RFC 8032 text by a PowerShell parse,
  not retyped. Measured: 56 Ed448 tests, about 0.5 s; the library at 100% line and branch,
  663 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Ed448 key derivation, signing and verification (with context) pass every RFC 8032 section 7.4 vector, 100% covered
