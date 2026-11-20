---
id: AF-0908
title: Surviving mutant: the lib557 port can leave out every lowest-nibble unsigned printf case
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:Unsigned-ge:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:193:>=
task: BL-5623
tasks: BL-5623
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0908 - Surviving mutant: the lib557 port can leave out every lowest-nibble unsigned printf case

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:193`: Surviving mutant: the lib557 port can leave out every lowest-nibble unsigned printf case.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:193`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 912464: 'for (int shift = bits - 4; shift >= 0; shift -= 4)' in Unsigned mutated to 'shift > 0' survived. The table's own comment says it holds each of the nibbles F, E, C and 1 at every nibble position. The mutant drops the shift-0 rows (0xF, 0xE, 0xC, 0x1) from every width, so the port checks fewer curl_mprintf cases than upstream's lib557, and a formatting bug in the lowest digit would go unseen.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib557.cs:193:>= -Member Unsigned -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
