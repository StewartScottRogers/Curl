---
id: AF-0996
title: Surviving mutant: the multi handle can treat a transfer whose deadline is exactly now as still settled (<= -> <)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:IsSettled-le:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:273:<=
task: BL-5990
tasks: BL-5990
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0996 - Surviving mutant: the multi handle can treat a transfer whose deadline is exactly now as still settled (<= -> <)

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:273`: Surviving mutant: the multi handle can treat a transfer whose deadline is exactly now as still settled (<= -> <).

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:273`

Mutant '!(entry.SocketWait!.EarliestDeadline <= timeProvider.GetUtcNow())' -> '!(... < ...)' survived. IsSettled's doc says a transfer whose deadline has passed is let go on until its own timer fails it with OperationTimedOut (CGR-034 C_75); with the mutant a deadline equal to the clock is not yet passed, so a socket_action at exactly the deadline tick does not let the timeout run. Tests use manual clocks but none stops at the exact deadline.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:273:<= -Member IsSettled -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-25: filed proposed.
