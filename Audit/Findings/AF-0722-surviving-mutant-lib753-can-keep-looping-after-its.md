---
id: AF-0722
title: Surviving mutant: lib753 can keep looping after its test timeout aborts
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:StartAndLoopAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144:false
task: BL-5081
tasks: BL-5081
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
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

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (return (false, 0) -> (true, 0) in StartAndLoopAsync). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
