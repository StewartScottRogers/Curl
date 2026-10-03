---
id: AF-0005
title: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:Decode-or-to-and:surviving-mutant
task: BL-1261
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0005 - Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test

## Summary

Medium finding from the quality auditor at `Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161`: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161`

Mutant survived (seed 0): `if (!domain.Field.IsBelowModulus(x) || !domain.Field.IsBelowModulus(y))` became `... && ...`. A point with only one out-of-range coordinate would then be accepted, which weakens the public-key validation decision. No test supplies exactly one out-of-range coordinate. Mutation score for the library was 0.95 (36 killed, 2 timed out, 2 survived).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-crypto.json
```

- Expected: The mutant at Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 (||) is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 ||

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Mutation run on Curl.Cryptography.UnitLibrary with seed 0 and 40 mutants: 34 killed, 2 timed out, 4 survived, score 0.9. No mutant was sampled at BrainpoolPoint.cs:161 (the || check is still in the source). The only Brainpool mutants sampled (BrainpoolPoint.cs:163 and others) were killed. Survivors were in SortingNetwork.cs, MlDsaPolynomial.cs, MontgomeryModulus.cs and AeadAesCcm.cs. The site was not sampled, so the finding is neither confirmed nor shown fixed.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | Cryptography run, seed 0: killed 19, timedOut 4, survived 0, score 1. BrainpoolPoint.cs:163 mutant timed out (counted as killed).
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | Seed-0 run sampled BrainpoolPoint.cs:163 (the || below-modulus check) and it was killed.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
