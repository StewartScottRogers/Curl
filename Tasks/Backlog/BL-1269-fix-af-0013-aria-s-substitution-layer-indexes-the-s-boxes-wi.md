---
id: BL-1269
title: Fix AF-0013: ARIA's substitution layer indexes the S-boxes with key-mixed state bytes (TLS ARIA-GCM suites)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1269 — Fix AF-0013: ARIA's substitution layer indexes the S-boxes with key-mixed state bytes (TLS ARIA-GCM suites)

## Goal

The defect the audit office reported as AF-0013 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0013 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0013-aria-s-substitution-layer-indexes-the-s-boxes-with.md`.

Location: `Curl.Cryptography.UnitLibrary/Aria.cs:201`

Location: `Curl.Cryptography.UnitLibrary/Aria.cs:201`

Line 201: `bytes[index] = SubstitutionBoxes[(((index + firstBox) % 4) * 256) + bytes[index]];` where bytes is the state after XOR with a secret round key (line 307), so the table address depends on the key. Used by AeadAriaGcm for TLS's ARIA-GCM suites. Phase 2: explained by the class's XML docs (line 15, 'Not constant-time') and ADR-0147; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Aria.cs -Pattern 'SubstitutionBoxes\[\('
```

- Expected: No match: the substitution does not read a table at a secret index.
- Actual: Aria.cs:201:                bytes[index] = SubstitutionBoxes[(((index + firstBox) % 4) * 256) + bytes[index]];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
