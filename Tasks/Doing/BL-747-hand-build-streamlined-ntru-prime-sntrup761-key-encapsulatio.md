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
completed:
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

- [ ] `Sntrup761.GenerateKeyPair`, `Encapsulate` and `Decapsulate` exist with span-based signatures and overloads that take the random bytes, per ADR-0118.
- [ ] Tests reproduce the round-3 submission's KAT (`kat_kem.rsp` for sntrup761, first vectors) byte for byte, citing the source beside each vector.
- [ ] A tampered ciphertext decapsulates to the implicit-rejection secret, not an exception, and a test pins it.
- [ ] Coverage 100% line and branch for the new type, no method over complexity 10, `dotnet build` clean and fast tests green.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
