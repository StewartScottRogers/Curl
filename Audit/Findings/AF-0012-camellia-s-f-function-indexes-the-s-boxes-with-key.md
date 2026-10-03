---
id: AF-0012
title: Camellia's F-function indexes the S-boxes with key-mixed data (TLS Camellia suites)
auditor: security
severity: High
status: closed
reason: Re-audit 2026-10-03_0623.md: the reproduction no longer reproduces.
key: security:Curl.Cryptography.UnitLibrary/Camellia.cs:F:secret-dependent-lookup
task: BL-1268
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed: 2026-10-03
closed-by: 2026-10-03_0623.md
---
# AF-0012 - Camellia's F-function indexes the S-boxes with key-mixed data (TLS Camellia suites)

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/Camellia.cs:304`: Camellia's F-function indexes the S-boxes with key-mixed data (TLS Camellia suites).

## Evidence

Location: `Curl.Cryptography.UnitLibrary/Camellia.cs:304`

Lines 304-310: `uint t1 = box[(int)(x >> 56)];` and six more box[...] reads where x is the round input XOR the secret subkey, so the memory address depends on key and plaintext bytes (cache-timing). Used for TLS's Camellia CBC suites (RFC 5932). Phase 2: explained by the class's XML docs (line 16, 'Not constant-time') and ADR-0145, which accepts this by design; kept for triage.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Camellia.cs -Pattern 'box\[\(int\)\(x >> 56\)\]'
```

- Expected: No match: S-box values are selected by a masked scan or bitsliced, not read at a key-dependent index.
- Actual: Camellia.cs:304:        uint t1 = box[(int)(x >> 56)];

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Select-String -Path Curl.Cryptography.UnitLibrary/Camellia.cs -Pattern 'box\[\(int\)\(x >> 56\)\]' found no match. Camellia.SubstituteBytes (Camellia.cs:335-350) now reads all 256 SBOX1 entries in order and keeps each by a branch-free per-byte equality mask; no address depends on the key-mixed bytes.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-03: accepted -> closed. Re-audit 2026-10-03_0623.md: the reproduction no longer reproduces.
