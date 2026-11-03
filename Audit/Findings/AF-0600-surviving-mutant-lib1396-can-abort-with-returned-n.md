---
id: AF-0600
title: Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:Escape-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134:<
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0600 - Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134`: Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134`

taken < 0 -> taken <= 0 survived: a row whose escaped length is 0 then gives null and the program aborts, changing the lib1396 output the upstream case compares.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134:< -Member Escape -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
