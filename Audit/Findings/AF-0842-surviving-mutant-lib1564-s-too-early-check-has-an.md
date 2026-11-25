---
id: AF-0842
title: Surviving mutant: lib1564's too-early check has an untested boundary
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:PollAsync-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:89:<
task: BL-5362
tasks: BL-5362
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-18
closed-how: mechanical
closed-by: 2026-11-18_0810.md
---
# AF-0842 - Surviving mutant: lib1564's too-early check has an untested boundary

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:89`: Surviving mutant: lib1564's too-early check has an untested boundary.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:89`

bool tooEarly = !endsEarly && waited < ThresholdMilliseconds; -> waited <= ThresholdMilliseconds survived. A poll returning after exactly the threshold would then be refused with 'curl_multi_poll returned too early' and TEST_ERR_MAJOR_BAD. The clock is an injected TimeProvider, so the boundary is testable, but Lib1564_OnAPollOnTheWrongSide_WritesItAndEndsMajorBad never hits it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:89:< -Member PollAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the -Site command: Lib1564.cs:89:< [PollAsync] 'waited < ThresholdMilliseconds' -> '<=' resolvedLine 89, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-17: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
