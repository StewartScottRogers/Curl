---
id: AF-0016
title: RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM)
auditor: security
severity: High
status: closed
reason: Re-audit 2026-10-03_0623.md: the reproduction no longer reproduces.
key: security:Curl.Cryptography.UnitLibrary/Rc4.cs:ApplyKeyStream:secret-dependent-lookup
task: BL-1272
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed: 2026-10-03
closed-by: 2026-10-03_0623.md
---
# AF-0016 - RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM)

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/Rc4.cs:113`: RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM).

## Evidence

Location: `Curl.Cryptography.UnitLibrary/Rc4.cs:113`

Line 113: `return permutation[(byte)(permutation[first] + permutation[second])];` - the permutation and the index both derive from the secret key, so the memory address is secret. Phase 2: explained by the class's XML docs (line 13, 'Not constant-time'), listed as by design in the library's CLAUDE.md; kept for triage.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Rc4.cs -Pattern 'permutation\[\(byte\)'
```

- Expected: No match.
- Actual: Rc4.cs:113:        return permutation[(byte)(permutation[first] + permutation[second])];

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Select-String -Path Curl.Cryptography.UnitLibrary/Rc4.cs -Pattern 'permutation\[\(byte\)' found no match. Rc4.SwapWithSecretIndex and ReadAtSecretIndex rewrite/read all 256 entries by mask; the only direct index left, permutation[first], uses the public counter i.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-03: accepted -> closed. Re-audit 2026-10-03_0623.md: the reproduction no longer reproduces.
