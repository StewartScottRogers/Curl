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
completed: 2026-10-07
---
# BL-1711 — Run MontgomeryModulus multiplication for the brainpool curves and RSA on 64-bit limbs

## Goal

`MontgomeryModulus.Multiply`, `ToMontgomeryForm` and `MultiplyModulo` run on 64-bit limbs as `Exponentiate` does since BL-1656, so brainpool ECDH/ECDSA and RSA-CRT signing get the same speed-up, staying constant-time.

## Context

- BL-1656 moved only `Exponentiate` to 64-bit limbs (`MultiplyWide`, `SquareWide`, carries from unsigned comparisons, ADR-0429); every other `MontgomeryModulus` member still runs the 32-bit CIOS loop through `AddProduct` and `ReduceOneLimb`.
- Callers (`BrainpoolDomainParameters`, `BrainpoolPoint`, `RsaCrtPrivateKey`, `DsaSignature`, `Scalar448`) pass 32-bit limb spans and scratch of `LimbCount + 2` limbs; either keep that surface and convert at the edges, or move the callers to 64-bit limbs.
- File: `Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs`.

## Acceptance criteria

- [x] One brainpoolP512r1 `BrainpoolEcdh` key generation plus agreement is at least 1.5 times faster, measured before and after on the same machine (numbers in Notes).
- [x] No branch, loop bound or index depends on a secret, stated in the doc comments.
- [x] `dotnet build` is clean, the fast tests pass, and `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage.
## Notes

- Before and after on the same machine, a file-based app outside the repository (best of 5 x 10 runs of two `GeneratePrivateKey` + `ComputePublicKey` and one `TryComputeSharedSecret` on brainpoolP512r1): **16.59 ms before, 5.67 ms after, 2.9 times faster.**
- Choice: the 32-bit surface stays, so no caller changes. With an even `LimbCount` the 64-bit radix R' equals R, so `Multiply` reads the caller's 32-bit spans in place as 64-bit limbs (`MemoryMarshal.Cast`, no copy; every .NET target is little-endian) and runs the existing `MultiplyWide`; the `LimbCount + 2` scratch is exactly the m + 1 64-bit limbs it needs. `ToMontgomeryForm` and `MultiplyModulo` go through `Multiply` and speed up with it. An odd limb count (DSA's 160-bit q, for one) keeps the 32-bit CIOS loop (`MultiplyNarrow`), because there R' = 2^32 R and the callers' Montgomery-form constants would change. The brainpool curves (8, 12, 16 limbs) and RSA primes of whole 64-bit words take the fast path.
- Constant time: the only new branch is on the limb count's parity, which is public; stated in `Multiply`'s remarks.
- Tests: `Multiply_OddLimbCount_GivesTheProductTimesRInverseOnTheThirtyTwoBitLoop` (2^89 - 1, 3 limbs) pins the 32-bit path; the existing 4-limb tests and every brainpool, RSA and DSA vector pin the 64-bit one. Measure-CodeQuality: `Curl.Cryptography.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Multiply on 64-bit limbs for even limb counts; P512r1 ECDH 2.9x faster
