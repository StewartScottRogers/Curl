---
id: BL-675
title: Hand-build the MD4 and RIPEMD-160 hashes and HMAC-RIPEMD-160
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-675 — Hand-build the MD4 and RIPEMD-160 hashes and HMAC-RIPEMD-160

## Goal

`Curl.Cryptography.UnitLibrary` computes MD4 (RFC 1320), RIPEMD-160 and HMAC-RIPEMD-160 (RFC 2286), matching their published vectors.

## Context

- Consumers: MD4 for the NTLM hash (MS-NLMP `NTOWFv1`, BL-684) and Kerberos `rc4-hmac` string-to-key (RFC 4757, BL-686); HMAC-RIPEMD-160 for SSH `hmac-ripemd160` and `hmac-ripemd160@openssh.com` (libssh2 1.11.1 offers them; BL-680). The BCL has neither. API and rules: BL-669's ADR.
- Vectors: RFC 1320 appendix A.5 (MD4 test suite); the RIPEMD-160 authors' page (Bosselaers) for the hash; RFC 2286 section 2 for HMAC-RIPEMD-160.
- Neither is a secret-keyed primitive on its own, so constant time applies only to the HMAC key handling.

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass all seven RFC 1320 A.5 vectors, the RIPEMD-160 reference vectors (including the million-`a` one, or mark it `TestCategory=Integration` if it is slow), and all RFC 2286 HMAC-RIPEMD-160 vectors, plus incremental hashing split across calls.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- API follows ADR-0118 and the BCL's `IncrementalHash` shape: `Md4`, `Ripemd160` and
  `HmacRipemd160` are sealed `IDisposable` classes with a static one-shot `HashData`
  and incremental `AppendData` / `GetHashAndReset`; `HmacRipemd160` adds a fixed-time
  `Verify`. Destinations must be exactly `HashSize` bytes (as `Poly1305` does), so a
  truncated MAC is the caller's slice of the full 20 bytes. Also like `Poly1305`, the
  one-shot methods have no `Try` form.
- MD4 and RIPEMD-160 share one internal Merkle-Damgard core,
  `LittleEndianMerkleDamgard<T>`, over the internal `ILittleEndianCompressionFunction`
  (static abstract members, so AOT-friendly and no delegates). Each public hash
  implements that interface explicitly, so there is still one public type per primitive.
- The million-`a` RIPEMD-160 vector runs in milliseconds, so it stays in the fast run.
- Every vector passed first time: RFC 1320 A.5 (7), Bosselaers' page (8 + million `a`),
  RFC 2286 section 2 (7).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Cryptography computes MD4, RIPEMD-160 and HMAC-RIPEMD-160, one-shot and incremental, pinned by RFC 1320, Bosselaers' and RFC 2286 vectors
