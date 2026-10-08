---
id: BL-1711
title: Run MontgomeryModulus multiplication for the brainpool curves and RSA on 64-bit limbs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1711 — Run MontgomeryModulus multiplication for the brainpool curves and RSA on 64-bit limbs

## Goal

`MontgomeryModulus.Multiply`, `ToMontgomeryForm` and `MultiplyModulo` run on 64-bit limbs as `Exponentiate` does since BL-1656, so brainpool ECDH/ECDSA and RSA-CRT signing get the same speed-up, staying constant-time.

## Context

- BL-1656 moved only `Exponentiate` to 64-bit limbs (`MultiplyWide`, `SquareWide`, carries from unsigned comparisons, ADR-0429); every other `MontgomeryModulus` member still runs the 32-bit CIOS loop through `AddProduct` and `ReduceOneLimb`.
- Callers (`BrainpoolDomainParameters`, `BrainpoolPoint`, `RsaCrtPrivateKey`, `DsaSignature`, `Scalar448`) pass 32-bit limb spans and scratch of `LimbCount + 2` limbs; either keep that surface and convert at the edges, or move the callers to 64-bit limbs.
- File: `Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs`.

## Acceptance criteria

- [ ] One brainpoolP512r1 `BrainpoolEcdh` key generation plus agreement is at least 1.5 times faster, measured before and after on the same machine (numbers in Notes).
- [ ] No branch, loop bound or index depends on a secret, stated in the doc comments.
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage.
## Notes

## Log

- 2026-10-07: Created.
