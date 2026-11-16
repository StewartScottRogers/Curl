---
id: AF-0790
title: Surviving mutant: lib1560's UntilError can stop after the first passing row and skip every later check
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:UntilError-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177:>
task: BL-5241
tasks: BL-5241
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_1610.md
---
# AF-0790 - Surviving mutant: lib1560's UntilError can stop after the first passing row and skip every later check

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177`: Surviving mutant: lib1560's UntilError can stop after the first passing row and skip every later check.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177`

Mutant 'if (errors > 0)' -> 'if (errors >= 0)' in UntilError survived (seed 5851, -MaxMutants 40). The mutant returns after the first row whatever its result, so lib1560 (upstream test1560, the URL API conformance program) checks one row of each table and reports success. URL-parsing regressions in later rows would then pass the upstream case unseen. No Lib1560ChecksTests case feeds a passing row followed by a failing one.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177:> -Member UntilError -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-15 | 2026-11-15_1610.md | reproduces: no | Ran the -Site command: site Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:177:>, resolvedLine 177, outcome killed (killed 1, survived 0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
