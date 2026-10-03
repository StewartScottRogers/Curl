---
id: AF-0013
title: ARIA's substitution layer indexes the S-boxes with key-mixed state bytes (TLS ARIA-GCM suites)
auditor: security
severity: High
status: accepted
reason: 
key: security:Curl.Cryptography.UnitLibrary/Aria.cs:Substitute:secret-dependent-lookup
task: BL-1269
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0013 - ARIA's substitution layer indexes the S-boxes with key-mixed state bytes (TLS ARIA-GCM suites)

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/Aria.cs:201`: ARIA's substitution layer indexes the S-boxes with key-mixed state bytes (TLS ARIA-GCM suites).

## Evidence

Location: `Curl.Cryptography.UnitLibrary/Aria.cs:201`

Line 201: `bytes[index] = SubstitutionBoxes[(((index + firstBox) % 4) * 256) + bytes[index]];` where bytes is the state after XOR with a secret round key (line 307), so the table address depends on the key. Used by AeadAriaGcm for TLS's ARIA-GCM suites. Phase 2: explained by the class's XML docs (line 15, 'Not constant-time') and ADR-0147; kept for triage.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Aria.cs -Pattern 'SubstitutionBoxes\[\('
```

- Expected: No match: the substitution does not read a table at a secret index.
- Actual: Aria.cs:201:                bytes[index] = SubstitutionBoxes[(((index + firstBox) % 4) * 256) + bytes[index]];

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
