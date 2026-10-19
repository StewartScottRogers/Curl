---
id: AF-0260
title: LibTestContext's null-argument check >= 0 can become > 0 with no test failing, accepting a null first argument
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:LibTestContext-ge:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25:>=
task: BL-2771
tasks: BL-2771
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1905.md
---
# AF-0260 - LibTestContext's null-argument check >= 0 can become > 0 with no test failing, accepting a null first argument

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25`: LibTestContext's null-argument check >= 0 can become > 0 with no test failing, accepting a null first argument. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25`

LibTest sample, seed 0: survived. Original `if (Array.IndexOf(copy, null) >= 0) throw new ArgumentNullException(...)`, mutated `> 0`. A null at index 0, which is the URL every port reads, is no longer refused. No test passes a null first argument.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/LibTestContext.cs:25:>= -Member LibTestContext -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the -Site command: line 25, Array.IndexOf(copy, null) >= 0 -> > 0 killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
