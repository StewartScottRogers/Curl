---
id: BL-2572
title: Fix AF-0235: ML-KEM's FIPS 203 modulus check can accept a first coefficient equal to q with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-17
completed:
---
# BL-2572 — Fix AF-0235: ML-KEM's FIPS 203 modulus check can accept a first coefficient equal to q with no test failing

## Goal

The defect the audit office reported as AF-0235 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0235 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0235-ml-kem-s-fips-203-modulus-check-can-accept-a-first.md`.

Location: `Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202`

Location: `Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202`

Mutant 'if (first >= Modulus || second >= Modulus)' -> 'if (first > Modulus || second >= Modulus)' survived. An encapsulation key whose even-indexed 12-bit coefficient is exactly 3329 would pass the check in MlKem.cs:173 (FIPS 203 section 7.2 encapsulation-key check) instead of being refused. The tests cover the odd-indexed coefficient and values above q, but not first == q.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/MlKemPolynomial.cs:202:>= -Member AreAllBelowModulus -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
