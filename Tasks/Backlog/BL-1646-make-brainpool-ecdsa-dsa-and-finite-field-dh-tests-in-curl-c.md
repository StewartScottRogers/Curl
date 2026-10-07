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
completed:
---
# BL-1646 — Make Brainpool ECDSA, DSA and finite-field DH tests in Curl.Cryptography.UnitTests finish under the 3-second budget

## Goal

`BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult`, the largest data row of `DsaSignatureTests.VerifyHash_CavpSigVer_GivesPublishedResult`, and `FiniteFieldDiffieHellmanTests.Generate_TwoParties_AgreeOnTheSharedSecret` and `ComputePublicValueAndSharedSecret_FixedExponents_EqualBigIntegerModPow` no longer print a `SLOW:` line (ADR-0417's 3-second budget) in the fast run, by making the library code faster, without removing a test vector or data row.

## Context

- Found by BL-1464 on 2026-10-07 (Debug build, run alone, while other lanes built): Brainpool P256r1 Wycheproof 3588 ms (`PHASE verify every vector`; BL-1559 already sped it up once), DSA CAVP SigVer largest row 3431 ms (`PHASE verify`), FFDH `Generate_TwoParties` up to 5101 ms and `ComputePublicValueAndSharedSecret_FixedExponents` up to 3290 ms for the largest groups (`PHASE public values and shared secrets`).
- Start with a profile of each: the modular exponentiation and point arithmetic in `Curl.Cryptography.UnitLibrary`. The SLOW total for the FFDH tests is larger than their `PHASE` line, so check where the arrange time goes too.
- Measure on an idle machine as well as under lanes before declaring a test fixed; record both.

## Acceptance criteria

- [ ] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~BrainpoolEcdsaTests|FullyQualifiedName~DsaSignatureTests|FullyQualifiedName~FiniteFieldDiffieHellmanTests" --logger "console;verbosity=detailed"` prints no `SLOW:` line on an idle machine; timings before and after are in Notes.
- [ ] The numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches in `Curl.Cryptography.UnitTests` are no lower than before.
- [ ] `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10 and CRAP at most 30.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-07: Created.
