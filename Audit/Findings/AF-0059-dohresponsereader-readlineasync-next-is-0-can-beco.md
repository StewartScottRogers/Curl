---
id: AF-0059
title: DohResponseReader.ReadLineAsync: 'next is >= 0' can become '> 0' with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DohResponseReader.cs:ReadLineAsync-ge:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DohResponseReader.cs:182:>=
task: BL-1702
tasks: BL-1702
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: mechanical
closed-by: 2026-10-08_0748.md
---
# AF-0059 - DohResponseReader.ReadLineAsync: 'next is >= 0' can become '> 0' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DohResponseReader.cs:182`: DohResponseReader.ReadLineAsync: 'next is >= 0' can become '> 0' with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:182`

Mutant: while (next is >= 0 and not '\n' && ...) -> while (next is > 0 and not '\n' && ...). It survived the sampled run (seed 0). With the mutant, a NUL byte in a DoH response's status or header line stops the line early and ReadLineAsync returns null. The DoH lookup then fails instead of reading the head, which changes a refused input. No test sends a head line containing a 0x00 byte.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohResponseReader.cs:182:>= -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the -Site command: killed at DohResponseReader.cs:182 ('next is >= 0' -> '> 0'). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
