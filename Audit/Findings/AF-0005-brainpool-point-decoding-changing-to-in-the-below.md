---
id: AF-0005
title: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:TryDecode-or:surviving-mutant
reproduction: mutation Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161:||
task: BL-1261
tasks: BL-1261
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed: 2026-10-07
closed-how: mechanical
closed-by: 2026-10-07_1336.md
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161:|| -Member TryDecode -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 (||) is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 ||

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Mutation run on Curl.Cryptography.UnitLibrary with seed 0 and 40 mutants: 34 killed, 2 timed out, 4 survived, score 0.9. No mutant was sampled at BrainpoolPoint.cs:161 (the || check is still in the source). The only Brainpool mutants sampled (BrainpoolPoint.cs:163 and others) were killed. Survivors were in SortingNetwork.cs, MlDsaPolynomial.cs, MontgomeryModulus.cs and AeadAesCcm.cs. The site was not sampled, so the finding is neither confirmed nor shown fixed.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | Cryptography run, seed 0: killed 19, timedOut 4, survived 0, score 1. BrainpoolPoint.cs:163 mutant timed out (counted as killed).
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | Seed-0 run sampled BrainpoolPoint.cs:163 (the || below-modulus check) and it was killed.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | The seed-0 tool run at this commit did not sample BrainpoolPoint.cs:161, so the mutant was applied by hand in a scratch export (git archive HEAD): ' || ' -> ' && ' in 'if (!domain.Field.IsBelowModulus(x) || !domain.Field.IsBelowModulus(y))'. Killed: 3 failures, TryComputeSharedSecret_PeerPointWithOnlyYRaisedByThePrime_ReturnsFalseAndZeroesTheSecret for BrainpoolP256r1, P384r1 and P512r1.
- 2026-10-07 | 2026-10-07_1336.md | reproduces: no | Ran the -Site reproduction: resolvedLine 161, outcome killed (score 1). No baseline failures were excluded in Curl.Cryptography.UnitTests. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
