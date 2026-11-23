---
id: BL-5787
title: Fix AF-0985: Lib5000And5004_SetTheSigningOptionsOnTheTransfer checks the signing key and key id only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5787 — Fix AF-0985: Lib5000And5004_SetTheSigningOptionsOnTheTransfer checks the signing key and key id only for null

## Goal

The defect the audit office reported as AF-0985 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0985 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:348`

Location: `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:348`

Step 1 (seeded sample of 50). The test pins the algorithm (Assert.AreEqual("ed25519", seen!.Authentication.HttpSignatureAlgorithm)), but the key and key id it says are set get only Assert.IsNotNull(seen.Authentication.HttpSignatureKey) and Assert.IsNotNull(seen.Authentication.HttpSignatureKeyId). The ports hard-code upstream lib5000/lib5004's key and key id, so a port that passed a wrong key or key id would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs -Pattern 'IsNotNull\(seen.Authentication.HttpSignature'
```

- Expected: No match: the key and key id are compared with the values lib5000.c and lib5004.c set.
- Actual: Lib3101To5004Tests.cs:348 Assert.IsNotNull(seen.Authentication.HttpSignatureKey); Lib3101To5004Tests.cs:349 Assert.IsNotNull(seen.Authentication.HttpSignatureKeyId);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test-only change: the key and key id are now compared with the values lib5000.c and lib5004.c set. Build clean, LibTest fast tests green (1242).

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-22: Doing -> Done. Key and key id asserted against the lib5000/lib5004 values
