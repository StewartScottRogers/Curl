---
id: BL-740
title: Hand-build X448 key agreement to RFC 7748
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-740 — Hand-build X448 key agreement to RFC 7748

## Goal

`Curl.Cryptography.UnitLibrary` computes X448 public keys and shared secrets exactly as RFC 7748 specifies, in constant time, with the field arithmetic (GF(2^448 - 2^224 - 1)) in its own type so Ed448 (BL-741) reuses it.

## Context

- ADR-0118 (BL-669): the BCL has no Curve448 on any platform. OpenSSL, the TLS library of the Linux and macOS reference curl, offers `x448` in its default TLS groups (`openssl list -tls-groups`, OpenSSL 3.5.7, 2026-09-28), so a server may pick it.
- Consumers: TLS 1.3 key share `x448` in the hand-built TLS client (BL-699), selectable with `--curves` (BL-709).
- Vectors: RFC 7748 section 5.2 (the two X448 single-step vectors, the 1 and 1,000 iteration vectors), section 6.2 (Alice's and Bob's keys and shared secret). An all-zero result is reported exactly as `X25519` reports it (ADR-0118).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass RFC 7748 section 5.2's X448 single-step vectors and the 1 and 1,000 iteration vectors (the 1,000,000 one, if present, marked `TestCategory=Integration`), and section 6.2's Alice/Bob keys and shared secret.
- [x] A low-order public key yields the all-zero result, reported as ADR-0118 says; no secret-dependent branch or table index in the ladder, stated in the XML docs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Field elements are 28 signed 16-bit limbs, the same shape as `Field25519`, so the reduction is two additions per folded limb (2^448 = 2^224 + 1 mod p) and Ed448 (BL-741) can reuse `Field448` as it is.
- The API mirrors `X25519` exactly (`KeySize`, `GeneratePrivateKey`, `ComputePublicKey`, `TryComputeSharedSecret` returning `false` with a zeroed secret), which is how ADR-0118 says an all-zero result is reported; no new ADR was needed.
- The 1,000,000-iteration vector is `TestCategory=Integration`; the 1 and 1,000 ones run in the fast suite (about 14 s together).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. X448 public keys and shared secrets match RFC 7748 sections 5.2 and 6.2, constant-time, 100% covered
