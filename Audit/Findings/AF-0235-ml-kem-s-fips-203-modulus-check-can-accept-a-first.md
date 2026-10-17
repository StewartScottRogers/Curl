---
id: AF-0235
title: ML-KEM's FIPS 203 modulus check can accept a first coefficient equal to q with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:AreAllBelowModulus-ge:surviving-mutant
reproduction: mutation Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202:>=
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0235 - ML-KEM's FIPS 203 modulus check can accept a first coefficient equal to q with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202`: ML-KEM's FIPS 203 modulus check can accept a first coefficient equal to q with no test failing. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202`

Mutant 'if (first >= Modulus || second >= Modulus)' -> 'if (first > Modulus || second >= Modulus)' survived. An encapsulation key whose even-indexed 12-bit coefficient is exactly 3329 would pass the check in MlKem.cs:173 (FIPS 203 section 7.2 encapsulation-key check) instead of being refused. The tests cover the odd-indexed coefficient and values above q, but not first == q.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202:>= -Member AreAllBelowModulus -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-16: filed proposed.
