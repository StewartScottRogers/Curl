---
id: AF-0728
title: Surviving mutant: a long-form DER length whose bytes exactly fill the input can be refused
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Output.UnitLibrary/DerReader.cs:TryReadLength-gt:surviving-mutant
reproduction: mutation Curl.Output.UnitLibrary/DerReader.cs:116:>
task: BL-5087
tasks: BL-5087
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0728 - Surviving mutant: a long-form DER length whose bytes exactly fill the input can be refused

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitLibrary/DerReader.cs:116`: Surviving mutant: a long-form DER length whose bytes exactly fill the input can be refused. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Output.UnitLibrary/DerReader.cs:116`

Mutant 'count > end - position' -> '>=' survived. When the length-of-length bytes end exactly at the buffer's end (for example 0x81 0x00 on an empty last element), the original reads the length and the mutant refuses the element. The boundary is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/DerReader.cs:116:> -Member TryReadLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command twice: the first stopped at a baseline build failure caused by a parallel build (CS0234 Curl.Api not found); the second, alone, gave outcome killed (count > end - position -> >=). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
