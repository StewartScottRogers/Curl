---
id: BL-1268
title: Fix AF-0012: Camellia's F-function indexes the S-boxes with key-mixed data (TLS Camellia suites)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1268 — Fix AF-0012: Camellia's F-function indexes the S-boxes with key-mixed data (TLS Camellia suites)

## Goal

The defect the audit office reported as AF-0012 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0012 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0012-camellia-s-f-function-indexes-the-s-boxes-with-key.md`.

Location: `Curl.Cryptography.UnitLibrary/Camellia.cs:304`

Location: `Curl.Cryptography.UnitLibrary/Camellia.cs:304`

Lines 304-310: `uint t1 = box[(int)(x >> 56)];` and six more box[...] reads where x is the round input XOR the secret subkey, so the memory address depends on key and plaintext bytes (cache-timing). Used for TLS's Camellia CBC suites (RFC 5932). Phase 2: explained by the class's XML docs (line 16, 'Not constant-time') and ADR-0145, which accepts this by design; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Camellia.cs -Pattern 'box\[\(int\)\(x >> 56\)\]'
```

- Expected: No match: S-box values are selected by a masked scan or bitsliced, not read at a key-dependent index.
- Actual: Camellia.cs:304:        uint t1 = box[(int)(x >> 56)];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
