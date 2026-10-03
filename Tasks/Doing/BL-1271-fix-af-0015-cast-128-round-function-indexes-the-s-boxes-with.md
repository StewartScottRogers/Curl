---
id: BL-1271
title: Fix AF-0015: CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1271 — Fix AF-0015: CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc)

## Goal

The defect the audit office reported as AF-0015 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0015 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0015-cast-128-round-function-indexes-the-s-boxes-with-k.md`.

Location: `Curl.Cryptography.UnitLibrary/Cast128.cs:242`

Location: `Curl.Cryptography.UnitLibrary/Cast128.cs:242`

Line 242: `uint s1 = boxes[(int)(input >> 24)];` (and line 244), where input is the half-block combined with the secret masking and rotation subkeys. Used for SSH's cast128-cbc. Phase 2: explained by the class's XML docs (line 17, 'Not constant-time'), which the library's CLAUDE.md lists as by design; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Cast128.cs -Pattern 'boxes\[\(int\)\(input >> 24\)\]'
```

- Expected: No match.
- Actual: Cast128.cs:242:        uint s1 = boxes[(int)(input >> 24)];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
