---
id: AF-0796
title: Surviving mutant: any IOException in a read-callback upload can be reported as an aborted-by-callback exit 42
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:FollowUnlessAbortedAsync-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371:&&
task: BL-5247
tasks: BL-5247
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_1610.md
---
# AF-0796 - Surviving mutant: any IOException in a read-callback upload can be reported as an aborted-by-callback exit 42

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371`: Surviving mutant: any IOException in a read-callback upload can be reported as an aborted-by-callback exit 42.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371`

Mutant 'exception is IOException && AnyReadAborted(reads)' -> '||' in FollowUnlessAbortedAsync survived (seed 5851). The predicate decides which escaping exceptions are a read callback's CURL_READFUNC_ABORT (upstream test1507). The mutant treats every IOException (a reset connection, a send failure) and every exception after an abort as AbortedByCallback, so a perform answers 42 'operation aborted by callback' in place of its real code.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371:&& -Member FollowUnlessAbortedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-15 | 2026-11-15_1610.md | reproduces: no | Ran the -Site command: site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371:&&, resolvedLine 371 (exception is IOException && AnyReadAborted -> ||), outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
