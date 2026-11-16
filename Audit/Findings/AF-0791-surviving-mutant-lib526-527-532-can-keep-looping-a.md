---
id: AF-0791
title: Surviving mutant: lib526/527/532 can keep looping after a select failure or a test timeout
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:TransferEachAsync-or:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88:||
task: BL-5242
tasks: BL-5242
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_1610.md
---
# AF-0791 - Surviving mutant: lib526/527/532 can keep looping after a select failure or a test timeout

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88`: Surviving mutant: lib526/527/532 can keep looping after a select failure or a test timeout.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88`

Mutant '!SelectTestAsync(...) || !steps.AbortOnTestTimeout(clock)' -> '... && ...' in TransferEachAsync survived. The mutant ends the loop only when both fail, so a select error alone or the test timeout alone no longer stops the program. A hung case would spin to the harness limit instead of ending with the program's error code. No test drives a failing select or an expired test clock through Lib526.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88:|| -Member TransferEachAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-15 | 2026-11-15_1610.md | reproduces: no | Ran the -Site command: site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:88:||, resolvedLine 88, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
