---
id: AF-0015
title: CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc)
auditor: security
severity: High
status: proposed
reason:
key: security:Curl.Cryptography.UnitLibrary/Cast128.cs:Round:secret-dependent-lookup
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0015 - CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc)

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/Cast128.cs:242`: CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc).

## Evidence

Location: `Curl.Cryptography.UnitLibrary/Cast128.cs:242`

Line 242: `uint s1 = boxes[(int)(input >> 24)];` (and line 244), where input is the half-block combined with the secret masking and rotation subkeys. Used for SSH's cast128-cbc. Phase 2: explained by the class's XML docs (line 17, 'Not constant-time'), which the library's CLAUDE.md lists as by design; kept for triage.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Cast128.cs -Pattern 'boxes\[\(int\)\(input >> 24\)\]'
```

- Expected: No match.
- Actual: Cast128.cs:242:        uint s1 = boxes[(int)(input >> 24)];

## Re-audits

## Log

- 2026-10-02: filed proposed.
