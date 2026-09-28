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
completed:
---
# BL-671 — Hand-build X25519 key agreement to RFC 7748

## Goal

`Curl.Cryptography.UnitLibrary` computes X25519 public keys and shared secrets exactly as RFC 7748 specifies, in constant time, with the field arithmetic (GF(2^255-19)) in its own type so Ed25519 (BL-672) reuses it.

## Context

- Consumers: SSH `curve25519-sha256` and `curve25519-sha256@libssh.org` (BL-678), TLS 1.3 and QUIC key share `x25519` (BL-699). API and rules: BL-669's ADR.
- RFC 7748 section 5 (the function, clamping, the Montgomery ladder), section 5.2 (test vectors including the 1 and 1,000 iteration vectors), section 6.1 (the Diffie-Hellman example with Alice's and Bob's keys).
- An all-zero shared secret must be reportable so SSH and TLS can reject it (RFC 7748 section 6.1, RFC 8731 section 3).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass RFC 7748 section 5.2's two single-step vectors and the 1 and 1,000 iteration vectors (the 1,000,000 one is optional and, if present, marked `TestCategory=Integration`), and section 6.1's Alice/Bob keys and shared secret.
- [ ] A low-order public key yields the all-zero result, and the API reports it as the ADR says.
- [ ] No secret-dependent branch or table index in the ladder (conditional swap by masking), stated in the XML docs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
