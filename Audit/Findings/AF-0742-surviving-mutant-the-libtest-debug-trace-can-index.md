---
id: AF-0742
title: Surviving mutant: the libtest debug trace can index past data that ends in a carriage return
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:EndsRowAt-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:123:<
task: BL-5187
tasks: BL-5187
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1335.md
---
# AF-0742 - Surviving mutant: the libtest debug trace can index past data that ends in a carriage return

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:123`: Surviving mutant: the libtest debug trace can index past data that ends in a carriage return.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:123`

Mutant noHex && index + 1 < data.Length && data[index] == CarriageReturn && data[index + 1] == LineFeed -> index + 1 <= data.Length survived (seed 641133). With noHex set and data whose last byte is CR, the mutant reads data[data.Length] and throws IndexOutOfRangeException, crashing the ported libtest's trace instead of writing the row as upstream's dump does. No LibTestDebugTrace test traces noHex data ending in a lone CR.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/LibTestDebugTrace.cs:123:< -Member EndsRowAt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the -Site command: LibTestDebugTrace.cs:123 '<' mutant outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-13: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
