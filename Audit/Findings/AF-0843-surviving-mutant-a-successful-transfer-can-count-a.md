---
id: AF-0843
title: Surviving mutant: a successful transfer can count as a connection cut off after its head
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:CutOffAfterHead-ne:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:296:!=
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0843 - Surviving mutant: a successful transfer can count as a connection cut off after its head

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:296`: Surviving mutant: a successful transfer can count as a connection cut off after its head.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:296`

result.ExitCode != CurlExitCode.Ok -> == survived in CutOffAfterHead, which feeds CountsAFailedConnection. A transfer with no report and a response code would be counted as a failed connection on success and not counted on failure. That changes the connection count Info reports, and no test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:296:!= -Member CutOffAfterHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
