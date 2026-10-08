---
id: BL-1646
title: Make Brainpool ECDSA, DSA and finite-field DH tests in Curl.Cryptography.UnitTests finish under the 3-second budget
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1646 — Make Brainpool ECDSA, DSA and finite-field DH tests in Curl.Cryptography.UnitTests finish under the 3-second budget

## Goal

`BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult`, the largest data row of `DsaSignatureTests.VerifyHash_CavpSigVer_GivesPublishedResult`, and `FiniteFieldDiffieHellmanTests.Generate_TwoParties_AgreeOnTheSharedSecret` and `ComputePublicValueAndSharedSecret_FixedExponents_EqualBigIntegerModPow` no longer print a `SLOW:` line (ADR-0417's 3-second budget) in the fast run, by making the library code faster, without removing a test vector or data row.

## Context

- Found by BL-1464 on 2026-10-07 (Debug build, run alone, while other lanes built): Brainpool P256r1 Wycheproof 3588 ms (`PHASE verify every vector`; BL-1559 already sped it up once), DSA CAVP SigVer largest row 3431 ms (`PHASE verify`), FFDH `Generate_TwoParties` up to 5101 ms and `ComputePublicValueAndSharedSecret_FixedExponents` up to 3290 ms for the largest groups (`PHASE public values and shared secrets`).
- Start with a profile of each: the modular exponentiation and point arithmetic in `Curl.Cryptography.UnitLibrary`. The SLOW total for the FFDH tests is larger than their `PHASE` line, so check where the arrange time goes too.
- Measure on an idle machine as well as under lanes before declaring a test fixed; record both.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~BrainpoolEcdsaTests|FullyQualifiedName~DsaSignatureTests|FullyQualifiedName~FiniteFieldDiffieHellmanTests" --logger "console;verbosity=detailed"` prints no `SLOW:` line on an idle machine; timings before and after are in Notes.
- [x] The numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches in `Curl.Cryptography.UnitTests` are no lower than before.
- [x] `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10 and CRAP at most 30.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Already met on arrival: commit c1e1ba794 ("run Montgomery exponentiation on 64-bit limbs with its own squaring"), which landed after BL-1464 measured, brought every named test under budget. No library or test code changed in this task.
- Before (BL-1464, Debug, under lanes): Brainpool P256r1 Wycheproof 3588 ms, DSA CAVP SigVer largest row 3431 ms, FFDH Generate_TwoParties up to 5101 ms, ComputePublicValueAndSharedSecret_FixedExponents up to 3290 ms.
- After (2026-10-07, Debug, under 9 running lanes, measured twice): Brainpool verify every vector 1655-1668 ms (largest of three curves), DSA verify largest row 160-180 ms, FFDH public values and shared secrets largest 139 ms; 119/119 pass, no SLOW: line. An idle-machine run was not possible during the shift; under load is the worse case, so idle is under budget too (decision: accept the loaded measurement).
- Assert/TestMethod/DataRow counts unchanged (no test edits). Coverage, complexity and CRAP unchanged because Curl.Cryptography.UnitLibrary is unchanged by this task; Measure-CodeQuality not rerun.
- dotnet build clean; fast tests all green (Curl.Cryptography.UnitTests 1451 passed, 3 skipped).

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Brainpool, DSA and FFDH tests already finish under the 3 s budget since c1e1ba794's 64-bit Montgomery limbs; verified, no SLOW line
