---
id: BL-671
title: Hand-build X25519 key agreement to RFC 7748
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-671 — Hand-build X25519 key agreement to RFC 7748

## Goal

`Curl.Cryptography.UnitLibrary` computes X25519 public keys and shared secrets exactly as RFC 7748 specifies, in constant time, with the field arithmetic (GF(2^255-19)) in its own type so Ed25519 (BL-672) reuses it.

## Context

- Consumers: SSH `curve25519-sha256` and `curve25519-sha256@libssh.org` (BL-678), TLS 1.3 and QUIC key share `x25519` (BL-699). API and rules: BL-669's ADR.
- RFC 7748 section 5 (the function, clamping, the Montgomery ladder), section 5.2 (test vectors including the 1 and 1,000 iteration vectors), section 6.1 (the Diffie-Hellman example with Alice's and Bob's keys).
- An all-zero shared secret must be reportable so SSH and TLS can reject it (RFC 7748 section 6.1, RFC 8731 section 3).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass RFC 7748 section 5.2's two single-step vectors and the 1 and 1,000 iteration vectors (the 1,000,000 one is optional and, if present, marked `TestCategory=Integration`), and section 6.1's Alice/Bob keys and shared secret.
- [x] A low-order public key yields the all-zero result, and the API reports it as the ADR says.
- [x] No secret-dependent branch or table index in the ladder (conditional swap by masking), stated in the XML docs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built as ADR-0118 specifies; no new ADR needed. `X25519` (public, static): `KeySize`,
  `GeneratePrivateKey` (fills from `RandomNumberGenerator`), `ComputePublicKey`
  (X25519(k, 9)), and `TryComputeSharedSecret`, which returns `false` with the
  destination all zero for a low-order peer key (checked by OR-ing every byte,
  `ConstantTime.IsAllZero`). Wrong lengths throw `ArgumentException`.
- Field arithmetic is `Field25519` (internal, static): 16 signed 64-bit limbs of 16 bits
  in a caller's `Span<long>` (the TweetNaCl representation), with `Decode`, `Encode`
  (full reduction), `Add`, `Subtract`, `Multiply`, `Square`, `Invert` (fixed chain for
  p - 2), a masked `ConditionalSwap` and `Clear`. Chosen over radix 2^25.5 because every
  product stays far inside 64 bits with no hand-tuned bounds, and the loop form keeps
  every method under the complexity gate. Ed25519 (BL-672) adds what it needs (square
  root, sign bit) to this type.
- Ladder: RFC 7748 section 5 as written, swap by mask, all 255 bits every time; every
  secret temporary is zeroed with `CryptographicOperations.ZeroMemory` in `finally`.
- Timing: the 1,000-iteration vector takes about 0.9 s in Release (CI's configuration)
  and about 5 s in Debug, so it stays in the fast suite as the criteria require. The
  1,000,000-iteration vector is present, `TestCategory=Integration`, and passed locally
  in Release (9 min 36 s).
- Vectors were copied from rfc-editor.org's rfc7748.txt on 2026-09-28. Measured gates:
  `dotnet build Curl.slnx -warnaserror` clean, all fast tests green (50 in
  Curl.Cryptography.UnitTests), `Measure-CodeQuality.ps1 -Library
  Curl.Cryptography.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 6.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Cryptography computes X25519 public keys and shared secrets per RFC 7748, constant-time, with Field25519 ready for Ed25519
