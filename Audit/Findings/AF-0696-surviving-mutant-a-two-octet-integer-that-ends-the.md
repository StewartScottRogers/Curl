---
id: AF-0696
title: Surviving mutant: a two-octet integer that ends the input can read as 0
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:TryReadUInt16-lt:surviving-mutant
reproduction: mutation Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34:<
task: BL-4890
tasks: BL-4890
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
---
# AF-0696 - Surviving mutant: a two-octet integer that ends the input can read as 0

## Summary

Medium finding from the quality auditor at `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34`: Surviving mutant: a two-octet integer that ends the input can read as 0.

## Evidence

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34`

'value = remaining.Length < 2 ? (ushort)0 : BinaryPrimitives.ReadUInt16BigEndian(remaining);' mutated to '<= 2' survived (drawn library, seed 982898). With exactly two bytes left, TrySkip(2) still succeeds but the value reads as 0. ObliviousDohConfig reads the version, KEM, KDF and AEAD this way (lines 55 and 73), so a final field could be misread and a config rejected or mis-selected.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34:< -Member TryReadUInt16 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: LengthPrefixedReader.cs:34 'remaining.Length < 2' -> '<= 2' was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
