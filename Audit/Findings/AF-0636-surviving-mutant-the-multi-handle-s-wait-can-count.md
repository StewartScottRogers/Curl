---
id: AF-0636
title: Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:WaitedSockets-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142:&&
task: BL-4815
tasks: BL-4815
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0636 - Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142`: Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'entries.FindAll(entry => !entry.Done && entry.Perform is not null)' -> '||'. Done transfers that have a perform, and pending ones not yet started, are counted among the running ones a curl_multi_wait waits on, which changes the wait's sockets and when it returns. No test waits with a finished or pending transfer beside a running one.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142:&& -Member WaitedSockets -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
