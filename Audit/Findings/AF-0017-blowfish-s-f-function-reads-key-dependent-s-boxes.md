---
id: AF-0017
title: Blowfish's F-function reads key-dependent S-boxes at data-dependent indexes (SSH blowfish-cbc, bcrypt_pbkdf)
auditor: security
severity: High
status: accepted
reason: 
key: security:Curl.Cryptography.UnitLibrary/BlowfishState.cs:F:secret-dependent-lookup
task: BL-1273
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0017 - Blowfish's F-function reads key-dependent S-boxes at data-dependent indexes (SSH blowfish-cbc, bcrypt_pbkdf)

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/BlowfishState.cs:133`: Blowfish's F-function reads key-dependent S-boxes at data-dependent indexes (SSH blowfish-cbc, bcrypt_pbkdf).

## Evidence

Location: `Curl.Cryptography.UnitLibrary/BlowfishState.cs:133`

Line 133: `((boxes[(int)(half >> 24)] + boxes[256 + (int)((half >> 16) & 0xFF)]) ^ boxes[512 + ...])`. The S-boxes are key-expanded, and in BcryptPbkdf the key is the SSH private-key passphrase, so the addresses depend on the passphrase. Phase 2: explained by Blowfish.cs's XML docs (line 14, 'Not constant-time') and the library's CLAUDE.md; kept for triage.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/BlowfishState.cs -Pattern 'boxes\[\(int\)\(half >> 24\)\]'
```

- Expected: No match.
- Actual: BlowfishState.cs:133:        ((boxes[(int)(half >> 24)] + boxes[256 + (int)((half >> 16) & 0xFF)]) ^ boxes[512 + (int)((half >> 8) & 0xFF)])

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
