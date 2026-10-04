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
completed: 2026-10-02
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `Aria.Substitute` now scans each of the four S-boxes whole with `Aria.SubstituteBytes` (the masked scan BL-1268 gave Camellia) over both 64-bit halves of the state, and keeps each box's bytes with a fixed lane mask that depends only on SL1/SL2. Recorded as ADR-0395 (supersedes ADR-0147's S-box choice). The XML remarks and the library's CLAUDE.md now say ARIA (and Camellia, missed by BL-1268) are constant-time. ADR-0147 itself is left as written, as BL-1268 left ADR-0145.
- Reproduction: `Select-String ... SubstitutionBoxes[(` now prints nothing. Build clean; fast tests green (Cryptography 1330 passed, RFC 5794 vectors included).

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. ARIA's substitution layers read their S-boxes by constant-time masked scan; AF-0013's reproduction no longer matches
