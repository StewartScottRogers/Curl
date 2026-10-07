---
id: BL-676
title: Hand-build the RC4 and CAST-128 ciphers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-676 — Hand-build the RC4 and CAST-128 ciphers

## Goal

`Curl.Cryptography.UnitLibrary` provides the RC4 stream cipher (with the keystream discard SSH's `arcfour128` needs) and the CAST-128 block cipher (with CBC), matching their published vectors.

## Context

- Consumers: SSH `arcfour`, `arcfour128` (RFC 4345: discard the first 1536 bytes) and `cast128-cbc`, all offered by libssh2 1.11.1 (BL-680); Kerberos `rc4-hmac` (RFC 4757, BL-686). The BCL has neither. API and rules: BL-669's ADR.
- Vectors: RFC 6229 (RC4 keystream at offsets for 40- to 256-bit keys); RFC 2144 Appendix B (CAST-128 single-plaintext vectors for 128-, 80- and 40-bit keys, and the maintenance test, optionally marked `TestCategory=Integration` if slow).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass the RFC 6229 vectors for at least the 40-, 128- and 256-bit keys at every listed offset, a 1536-byte discard case, and every RFC 2144 Appendix B.1 vector plus a CBC round trip.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- API follows `Blowfish` and ADR-0118, so no new ADR: `Rc4(key)` with `ApplyKeyStream`
  and `DiscardKeyStream(length)` (the caller discards, with `Rc4.Rfc4345DiscardLength`
  = 1536, rather than a constructor flag, so `arcfour`, `arcfour128`/`arcfour256` and
  Kerberos `rc4-hmac` share one type); `Cast128(key)` with ECB block and CBC methods of
  the same shape as `Blowfish`.
- Key sizes: RC4 takes 1 to 256 bytes (the whole permutation); CAST-128 takes 5 to 16
  bytes, zero-padded, 12 rounds up to 10 bytes and 16 above (RFC 2144 section 2.5).
  A key of 10 bytes zero-padded to 16 is therefore a different cipher from the 10-byte
  key, and a test pins that.
- The S-boxes were transcribed from the RFC 2144 text by a PowerShell script (not by
  hand) into `Cast128SubstitutionBoxes.cs`; the key schedule is section 2.4 as a
  32-row table run twice, which keeps every method under complexity 10.
- Tests: all 252 RFC 6229 rows (both key families, 40 to 256 bits, every offset, through
  `DiscardKeyStream`); every B.1 vector; the B.2 maintenance test (about 25 s, so
  `TestCategory=Integration`, passing); CBC pinned against ECB under a zero vector,
  against a hand-chained two-block computation, and round-tripped in place across calls.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` gives 100%
  line, 100% branch, 273 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Cryptography has Rc4 (with the RFC 4345 discard) and Cast128 (ECB and CBC), pinned to RFC 6229 and RFC 2144 Appendix B
