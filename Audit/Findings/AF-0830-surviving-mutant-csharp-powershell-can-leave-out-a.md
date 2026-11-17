---
id: AF-0830
title: Surviving mutant: --csharp/--powershell can leave out a -F form body of exactly the 1 MiB read-ahead limit
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-17_0430.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:ReadAhead-le:surviving-mutant
reproduction: mutation Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:87:<=
task: BL-5304
tasks: BL-5304
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed: 2026-11-17
closed-how: mechanical
closed-by: 2026-11-17_0430.md
---
# AF-0830 - Surviving mutant: --csharp/--powershell can leave out a -F form body of exactly the 1 MiB read-ahead limit

## Summary

Medium finding from the quality auditor at `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:87`: Surviving mutant: --csharp/--powershell can leave out a -F form body of exactly the 1 MiB read-ahead limit. Reported by an auditor flagged unreliable in 2026-11-16_1333.md.

## Evidence

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:87`

Mutation of a drawn library (seed 413266): 'stream.Length is not (> 0 and <= MaxReadAheadBytes)' mutated to '< MaxReadAheadBytes' survived. The only size test (CodeGenerationTextFormTests.cs:32) uses MaxReadAheadBytes + 1, so nothing pins that a body of exactly MaxReadAheadBytes (1024 * 1024) is still read. With the mutant such a form gives Body null, and the generated program sends no body.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:87:<= -Member ReadAhead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-17 | 2026-11-17_0430.md | reproduces: no | Ran the -Site command: resolvedLine 87, '<= MaxReadAheadBytes' -> '< MaxReadAheadBytes', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-17: accepted -> closed. Re-audit 2026-11-17_0430.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
