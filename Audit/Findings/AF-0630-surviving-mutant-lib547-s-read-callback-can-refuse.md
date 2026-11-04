---
id: AF-0630
title: Surviving mutant: lib547's read callback can refuse a buffer exactly the upload's size, and no test fails
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:Read-ge:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88:>=
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0630 - Surviving mutant: lib547's read callback can refuse a buffer exactly the upload's size, and no test fails

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88`: Surviving mutant: lib547's read callback can refuse a buffer exactly the upload's size, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'if (buffer.Length >= UploadThis.Length)' -> '>'. With a buffer exactly the data's length, the port writes 'READ NOT FINE!' and uploads nothing instead of 'READ!' and the data. That changes the program's stderr and the request body. No test reads with a buffer of that size.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:88:>= -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
