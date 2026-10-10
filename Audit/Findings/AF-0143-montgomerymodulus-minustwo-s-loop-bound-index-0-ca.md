---
id: AF-0143
title: MontgomeryModulus.MinusTwo's loop bound 'index >= 0' can become 'index > 0' with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:MinusTwo-ge:surviving-mutant
reproduction: mutation Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117:>=
task: none
tasks:
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0143 - MontgomeryModulus.MinusTwo's loop bound 'index >= 0' can become 'index > 0' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117`: MontgomeryModulus.MinusTwo's loop bound 'index >= 0' can become 'index > 0' with no test failing.

## Evidence

Location: `Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117`

Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0: survived, original 'for (int index = result.Length - 1; index >= 0; index--)' mutated '... index > 0 ...' in MinusTwo. The mutant never subtracts the borrow from the most significant byte, so any prime whose borrow runs to the top byte gets a wrong p-2: for 257 (bytes 01 01) it returns 01 FF = 511 instead of 255. MinusTwo is the Fermat inversion exponent for BrainpoolDomainParameters, DsaDomainParameters (subprime, which comes from a peer's DSA key) and RsaCrtPrivateKey (p and q from a key file). No test calls MinusTwo directly (grep finds no MinusTwo in Curl.Cryptography.UnitTests), and the curve and key vectors in use never borrow through to the top byte.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117:>= -Member MinusTwo -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
