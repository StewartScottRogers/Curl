---
id: AF-0014
title: DES round function indexes the S-boxes with key-dependent values (NTLM LMOWFv1 and DESL)
auditor: security
severity: High
status: proposed
reason:
key: security:Curl.Cryptography.UnitLibrary/Des.cs:Round:secret-dependent-lookup
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0014 - DES round function indexes the S-boxes with key-dependent values (NTLM LMOWFv1 and DESL)

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/Des.cs:170`: DES round function indexes the S-boxes with key-dependent values (NTLM LMOWFv1 and DESL).

## Evidence

Location: `Curl.Cryptography.UnitLibrary/Des.cs:170`

Line 170: `substituted = (substituted << 4) | SubstitutionBoxes[(box * 64) + (row * 16) + column];` where row and column come from the expanded half-block XOR the secret round key. NTLM keys DES with the password hash (LMOWFv1, DESL), so the address leaks password-derived bits. Phase 2: explained by the class's XML docs (line 16) and ADR-0156; kept for triage.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Des.cs -Pattern 'SubstitutionBoxes\[\(box \* 64\)'
```

- Expected: No match: S-box output is computed without a secret-indexed read.
- Actual: Des.cs:170:            substituted = (substituted << 4) | SubstitutionBoxes[(box * 64) + (row * 16) + column];

## Re-audits

## Log

- 2026-10-02: filed proposed.
