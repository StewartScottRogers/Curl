---
id: AF-0780
title: Surviving mutant: lib500 can write its timing-order complaint lines when two times are equal
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:WriteIfLater-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:118:>
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0780 - Surviving mutant: lib500 can write its timing-order complaint lines when two times are equal

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:118`: Surviving mutant: lib500 can write its timing-order complaint lines when two times are equal.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:118`

if (earlier > later) { file.WriteLine(...) } mutated to >= survived. Upstream lib500 writes the 'X vs Y' line only when the earlier time is larger. Equal times are common (a literal address has namelookup == connect), so the mutant would put lines into the libtest_arg2 file that upstream leaves empty, and no test checks that file for equal times.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:118:> -Member WriteIfLater -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
