---
id: BL-1270
title: Fix AF-0014: DES round function indexes the S-boxes with key-dependent values (NTLM LMOWFv1 and DESL)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1270 — Fix AF-0014: DES round function indexes the S-boxes with key-dependent values (NTLM LMOWFv1 and DESL)

## Goal

The defect the audit office reported as AF-0014 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0014 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0014-des-round-function-indexes-the-s-boxes-with-key-de.md`.

Location: `Curl.Cryptography.UnitLibrary/Des.cs:170`

Location: `Curl.Cryptography.UnitLibrary/Des.cs:170`

Line 170: `substituted = (substituted << 4) | SubstitutionBoxes[(box * 64) + (row * 16) + column];` where row and column come from the expanded half-block XOR the secret round key. NTLM keys DES with the password hash (LMOWFv1, DESL), so the address leaks password-derived bits. Phase 2: explained by the class's XML docs (line 16) and ADR-0156; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Des.cs -Pattern 'SubstitutionBoxes\[\(box \* 64\)'
```

- Expected: No match: S-box output is computed without a secret-indexed read.
- Actual: Des.cs:170:            substituted = (substituted << 4) | SubstitutionBoxes[(box * 64) + (row * 16) + column];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
