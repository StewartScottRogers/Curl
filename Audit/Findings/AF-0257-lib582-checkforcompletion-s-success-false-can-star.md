---
id: AF-0257
title: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'
auditor: quality
severity: Medium
status: closed
reason: Fixed by BL-2768 (Done 2026-10-18): Lib582Run.CheckForCompletion became CompletionOutcome with bool? success = null, so the success = false site no longer exists and three re-audits could only report site-missing.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:CheckForCompletion-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158:false
task: BL-2768
tasks: BL-2768
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-22
closed-how: stewart
closed-by: stewart
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

- 2026-10-18 | 2026-10-18_1905.md | not re-audited | Ran the -Site command: outcome site-missing. Lib582Run.cs now has CompletionOutcome with bool? success = null (line 159); no 'success = false' site remains in the member.
- 2026-10-19 | 2026-10-19_2134.md | not re-audited | Ran the -Site command: outcome site-missing (resolvedLine null). Lib582Run's completion check is now CompletionOutcome/RunUntilCompleteAsync (bool? success), with no CheckForCompletion member and no success = false site, so the mutant could not be run. Not re-audited.
- 2026-10-22 | 2026-10-22_0130.md | not re-audited | Ran the -Site reproduction: outcome site-missing (no mutant ran). Lib582Run.CheckForCompletion and its 'success = false' are gone. The port now uses CompletionOutcome, which returns a bool? that starts as null (Lib582Run.cs:158-170, its comment citing AF-0257). Not re-audited.
- 2026-10-22 | 2026-10-22_1120.md | not re-audited | Ran the reproduction: outcome site-missing, no mutant ran. Lib582Run.CheckForCompletion now starts 'bool? success = null' (line 159), with no 'false' site left in the member. Not re-audited.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-22: accepted -> closed. Stewart closed it: Fixed by BL-2768 (Done 2026-10-18): Lib582Run.CheckForCompletion became CompletionOutcome with bool? success = null, so the success = false site no longer exists and three re-audits could only report site-missing.
