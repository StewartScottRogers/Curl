---
id: AF-0796
title: Surviving mutant: any IOException in a read-callback upload can be reported as an aborted-by-callback exit 42
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:FollowUnlessAbortedAsync-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:371:&&
task: none
tasks:
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-15: filed proposed.
