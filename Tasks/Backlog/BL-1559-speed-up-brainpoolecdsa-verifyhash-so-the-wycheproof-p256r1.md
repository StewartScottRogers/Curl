---
id: BL-1559
title: Speed up BrainpoolEcdsa.VerifyHash so the Wycheproof P256r1 test fits the 3-second budget
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1559 — Speed up BrainpoolEcdsa.VerifyHash so the Wycheproof P256r1 test fits the 3-second budget

## Goal

`BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult` (in the fast run) finishes inside `TestDiagnostics`' 3000 ms budget, with no `SLOW:` line, by making `BrainpoolEcdsa.VerifyHash` faster, not by trimming vectors.

## Context

- Found by BL-1536 on 2026-10-07: its `PHASE verify every vector` took 3646 ms for 261 vectors (about 14 ms per verification in a Debug build, with other lanes building); the Integration-only P384r1 and P512r1 rows took 9.7 s and 24.6 s.
- Start in `Curl.Cryptography.UnitLibrary`'s Brainpool point arithmetic (`BrainpoolEcdsa`, `BrainpoolEcdh` and the field/point code they share): likely wins are a joint (Shamir) multi-scalar multiplication for u1*G + u2*Q, a fixed-base table for G, and avoiding per-operation `BigInteger` allocations. Keep every secret-dependent path constant time (ADR-0118); verification works on public data only.
- Test approach: the existing Wycheproof vectors are the correctness gate; no test changes beyond keeping them green.

## Acceptance criteria

- [ ] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.BrainpoolEcdsaTests." --logger "console;verbosity=detailed"` prints no `SLOW:` line for `VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult`.
- [ ] Every `BrainpoolEcdsaTests` and `BrainpoolEcdhTests` test still passes, including the Integration ones.
- [ ] `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage and no method above cyclomatic complexity 10.

## Notes

## Log

- 2026-10-07: Created.
