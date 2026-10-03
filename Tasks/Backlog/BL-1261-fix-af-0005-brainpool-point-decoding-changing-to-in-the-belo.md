---
id: BL-1261
title: Fix AF-0005: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1261 — Fix AF-0005: Brainpool point decoding: changing || to && in the below-modulus check is not caught by any test

## Goal

The defect the audit office reported as AF-0005 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0005 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0005-brainpool-point-decoding-changing-to-in-the-below.md`.

Location: `Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161`

Location: `Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161`

Mutant survived (seed 0): `if (!domain.Field.IsBelowModulus(x) || !domain.Field.IsBelowModulus(y))` became `... && ...`. A point with only one out-of-range coordinate would then be accepted, which weakens the public-key validation decision. No test supplies exactly one out-of-range coordinate. Mutation score for the library was 0.95 (36 killed, 2 timed out, 2 survived).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-crypto.json
```

- Expected: The mutant at Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 (||) is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/BrainpoolPoint.cs:161 ||

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
