---
id: BL-1962
title: Fix AF-0143: MontgomeryModulus.MinusTwo's loop bound 'index >= 0' can become 'index > 0' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-10
completed:
---
# BL-1962 — Fix AF-0143: MontgomeryModulus.MinusTwo's loop bound 'index >= 0' can become 'index > 0' with no test failing

## Goal

The defect the audit office reported as AF-0143 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0143 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0143-montgomerymodulus-minustwo-s-loop-bound-index-0-ca.md`.

Location: `Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117`

Location: `Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117`

Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0: survived, original 'for (int index = result.Length - 1; index >= 0; index--)' mutated '... index > 0 ...' in MinusTwo. The mutant never subtracts the borrow from the most significant byte, so any prime whose borrow runs to the top byte gets a wrong p-2: for 257 (bytes 01 01) it returns 01 FF = 511 instead of 255. MinusTwo is the Fermat inversion exponent for BrainpoolDomainParameters, DsaDomainParameters (subprime, which comes from a peer's DSA key) and RsaCrtPrivateKey (p and q from a key file). No test calls MinusTwo directly (grep finds no MinusTwo in Curl.Cryptography.UnitTests), and the curve and key vectors in use never borrow through to the top byte.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs:117:>= -Member MinusTwo -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
