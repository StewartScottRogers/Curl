---
id: BL-1273
title: Fix AF-0017: Blowfish's F-function reads key-dependent S-boxes at data-dependent indexes (SSH blowfish-cbc, bcrypt_pbkdf)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1273 — Fix AF-0017: Blowfish's F-function reads key-dependent S-boxes at data-dependent indexes (SSH blowfish-cbc, bcrypt_pbkdf)

## Goal

The defect the audit office reported as AF-0017 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0017 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0017-blowfish-s-f-function-reads-key-dependent-s-boxes.md`.

Location: `Curl.Cryptography.UnitLibrary/BlowfishState.cs:133`

Location: `Curl.Cryptography.UnitLibrary/BlowfishState.cs:133`

Line 133: `((boxes[(int)(half >> 24)] + boxes[256 + (int)((half >> 16) & 0xFF)]) ^ boxes[512 + ...])`. The S-boxes are key-expanded, and in BcryptPbkdf the key is the SSH private-key passphrase, so the addresses depend on the passphrase. Phase 2: explained by Blowfish.cs's XML docs (line 14, 'Not constant-time') and the library's CLAUDE.md; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/BlowfishState.cs -Pattern 'boxes\[\(int\)\(half >> 24\)\]'
```

- Expected: No match.
- Actual: BlowfishState.cs:133:        ((boxes[(int)(half >> 24)] + boxes[256 + (int)((half >> 16) & 0xFF)]) ^ boxes[512 + (int)((half >> 8) & 0xFF)])

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
