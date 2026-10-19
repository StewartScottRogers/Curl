---
id: AF-0258
title: Lib510 Read's buffer.Length < data.Length can become <= with no test failing, refusing a buffer that fits exactly
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:Read-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88:<
task: BL-2769
tasks: BL-2769
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1905.md
---
# AF-0258 - Lib510 Read's buffer.Length < data.Length can become <= with no test failing, refusing a buffer that fits exactly

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88`: Lib510 Read's buffer.Length < data.Length can become <= with no test failing, refusing a buffer that fits exactly. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88`

LibTest sample, seed 0: survived. Original `if (buffer.Length < data.Length)`, mutated `<=`. A read buffer exactly as long as the part would be refused with 'read buffer is too small to run test', and the upload would end early. No test covers the exact-fit edge.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88:< -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the -Site command: line 88, buffer.Length < data.Length -> <= killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
