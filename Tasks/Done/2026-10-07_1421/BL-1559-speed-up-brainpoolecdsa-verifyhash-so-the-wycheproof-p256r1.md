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
completed: 2026-10-07
---
# BL-1559 — Speed up BrainpoolEcdsa.VerifyHash so the Wycheproof P256r1 test fits the 3-second budget

## Goal

`BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult` (in the fast run) finishes inside `TestDiagnostics`' 3000 ms budget, with no `SLOW:` line, by making `BrainpoolEcdsa.VerifyHash` faster, not by trimming vectors.

## Context

- Found by BL-1536 on 2026-10-07: its `PHASE verify every vector` took 3646 ms for 261 vectors (about 14 ms per verification in a Debug build, with other lanes building); the Integration-only P384r1 and P512r1 rows took 9.7 s and 24.6 s.
- Start in `Curl.Cryptography.UnitLibrary`'s Brainpool point arithmetic (`BrainpoolEcdsa`, `BrainpoolEcdh` and the field/point code they share): likely wins are a joint (Shamir) multi-scalar multiplication for u1*G + u2*Q, a fixed-base table for G, and avoiding per-operation `BigInteger` allocations. Keep every secret-dependent path constant time (ADR-0118); verification works on public data only.
- Test approach: the existing Wycheproof vectors are the correctness gate; no test changes beyond keeping them green.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.BrainpoolEcdsaTests." --logger "console;verbosity=detailed"` prints no `SLOW:` line for `VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult`.
- [x] Every `BrainpoolEcdsaTests` and `BrainpoolEcdhTests` test still passes, including the Integration ones.
- [x] `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage and no method above cyclomatic complexity 10.

## Notes

- `BrainpoolPoint.Double` (Renes-Costello-Batina algorithm 3, complete doubling for any a):
  13 field multiplications against `Add`'s 17, branch-free, so `MultiplyScalar` uses it
  too and signing and ECDH stay constant-time while getting faster.
- `BrainpoolPoint.MultiplyAndAddPublic`: u1 * G + u2 * Q by Shamir's trick, one shared run
  of doublings, a 4-bit window of each scalar per four doublings, zero windows skipped.
  Variable-time by design: ECDSA verification works on public values only (ADR-0118,
  ADR-0217), so no ADR was needed. Only `BrainpoolEcdsa.Verify` calls it.
- Choice: no wNAF or cached generator table. The two changes above took the P256r1 phase
  from 2535 ms to about 1500 ms on this machine (P384r1 5.9 s, P512r1 13.6 s, from 9.7 s
  and 24.6 s), inside the 3000 ms budget with room for a loaded machine; the rest would
  add negation and recoding code for about 13% more.
- Measured: Brainpool tests 57/57 pass (Integration included); Measure-CodeQuality on
  Curl.Cryptography.UnitLibrary: line 100%, branch 100%, 0 failing members, worst CRAP 10.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Shamir's trick and complete doubling cut the P256r1 Wycheproof phase from 2535 ms to about 1500 ms; coverage 100/100.
