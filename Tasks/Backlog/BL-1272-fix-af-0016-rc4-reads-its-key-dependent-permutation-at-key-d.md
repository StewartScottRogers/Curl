---
id: BL-1272
title: Fix AF-0016: RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1272 — Fix AF-0016: RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM)

## Goal

The defect the audit office reported as AF-0016 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0016 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0016-rc4-reads-its-key-dependent-permutation-at-key-dep.md`.

Location: `Curl.Cryptography.UnitLibrary/Rc4.cs:113`

Location: `Curl.Cryptography.UnitLibrary/Rc4.cs:113`

Line 113: `return permutation[(byte)(permutation[first] + permutation[second])];` - the permutation and the index both derive from the secret key, so the memory address is secret. Phase 2: explained by the class's XML docs (line 13, 'Not constant-time'), listed as by design in the library's CLAUDE.md; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Rc4.cs -Pattern 'permutation\[\(byte\)'
```

- Expected: No match.
- Actual: Rc4.cs:113:        return permutation[(byte)(permutation[first] + permutation[second])];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
