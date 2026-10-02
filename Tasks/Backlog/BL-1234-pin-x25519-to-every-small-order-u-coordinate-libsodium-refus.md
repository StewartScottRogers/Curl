---
id: BL-1234
title: Pin X25519 to every small-order u-coordinate libsodium refuses, the order-8 points included
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1234 — Pin X25519 to every small-order u-coordinate libsodium refuses, the order-8 points included

## Goal

`X25519.TryComputeSharedSecret` is pinned, by test rows, to return `false` with an all-zero secret for every u-coordinate of small order on Curve25519, including the two order-8 points and p - 1 that the tests leave out today, and for each of them with bit 255 set.

## Context

- Today `Curl.Cryptography.UnitTests/X25519Tests.cs` `TryComputeSharedSecret_LowOrderPeerKey_ReturnsFalseWithAnAllZeroSecret` (rows around lines 83-89) covers 0, 1 and their non-canonical encodings p and p + 1 only.
- RFC 7748 section 6.1 (https://www.rfc-editor.org/rfc/rfc7748#section-6.1) says a protocol may check for the all-zero output, which every small-order point gives, and section 5 that the top bit of the u-coordinate is masked. The full list of small-order encodings is libsodium's `blacklist` in `crypto_scalarmult/curve25519/ref10/x25519_ref10.c` (its `has_small_order`), in little-endian hex:
  - `0000000000000000000000000000000000000000000000000000000000000000` (0, order 4)
  - `0100000000000000000000000000000000000000000000000000000000000000` (1, order 1)
  - `e0eb7a7c3b41b8ae1656e3faf19fc46ada098deb9c32b1fd866205165f49b800` (order 8)
  - `5f9c95bca3508c24b1d0b1559c83ef5b04445cc4581c8e86d8224eddd09f1157` (order 8)
  - `ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f` (p - 1, order 2)
  - `edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f` (p, that is 0)
  - `eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f` (p + 1, that is 1)
- Wycheproof's `x25519_test.json` also feeds these with bit 255 set (last byte `| 0x80`), which X25519 masks, so they must give the same result.
- This is a pinning task: the implementation is expected to pass already. If a row fails, fix `Curl.Cryptography.UnitLibrary/X25519.cs` so it does, and say what was wrong in `Notes`.

## Acceptance criteria

- [ ] `TryComputeSharedSecret_LowOrderPeerKey_ReturnsFalseWithAnAllZeroSecret` (or a sibling test beside it) has a row for each of the seven encodings above and for each of the seven with bit 255 set, with a comment naming libsodium's list and RFC 7748 sections 5 and 6.1; every row passes.
- [ ] The comment above the rows says which encodings are which point (as in the list above).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
