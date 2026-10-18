---
id: AF-0257
title: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:CheckForCompletion-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158:false
task: none
tasks:
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0257 - Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158`: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `success = false;`, mutated `true`. When no multi message arrives, the port reports success. It then skips writing 'Error uploading file.' and setting MajorBad, which changes the libtest's result and standard error that upstream case 582 judges.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158:false -Member CheckForCompletion -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
