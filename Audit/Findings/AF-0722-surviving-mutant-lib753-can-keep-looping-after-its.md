---
id: AF-0722
title: Surviving mutant: lib753 can keep looping after its test timeout aborts
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:StartAndLoopAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144:false
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0722 - Surviving mutant: lib753 can keep looping after its test timeout aborts

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144`: Surviving mutant: lib753 can keep looping after its test timeout aborts. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144`

Mutant 'return (false, 0);' -> 'return (true, 0);' survived. That return is taken when steps.AbortOnTestTimeout(clock) refuses. With the mutant the loop goes on after the 'ABORTING TEST' line, so the hang guard is lost and no test drives lib753 past its timeout.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144:false -Member StartAndLoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
