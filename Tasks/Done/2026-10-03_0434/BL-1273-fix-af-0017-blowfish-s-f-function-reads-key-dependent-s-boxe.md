---
id: BL-1273
title: Fix AF-0017: Blowfish's F-function reads key-dependent S-boxes at data-dependent indexes (SSH blowfish-cbc, bcrypt_pbkdf)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `BlowfishState.Mix` (the F function) now reads all 1,024 S-box words by one `Vector<uint>` masked scan, so no address depends on the key, passphrase or block; `Blowfish` and `BcryptPbkdf` are constant-time (ADR-0400). The reproduction finds no match.
- Added `Curl.Cryptography.UnitTests` to `touches` for the new `Mix` test against the direct look-up formula at every index; no other task in Doing names it.
- Default taken: a vectorised scan, not RC4's scalar one - scalar made one bcrypt hash take seconds. Loads use `Vector.LoadUnsafe` at fixed offsets so Debug test runs stay fast (span slicing was five times slower there). Cost: about 50 ms per bcrypt hash in Release.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Blowfish's F function reads its S-boxes by vectorised masked scan; AF-0017's reproduction no longer matches (ADR-0400)
