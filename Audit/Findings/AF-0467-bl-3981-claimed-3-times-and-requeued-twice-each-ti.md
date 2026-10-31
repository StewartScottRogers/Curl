---
id: AF-0467
title: BL-3981 claimed 3 times and requeued twice, each time on a missing dependency (BL-3976, then BL-3990)
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3981:redone-work
reproduction: none
task: BL-4185
tasks: BL-4185
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0467 - BL-3981 claimed 3 times and requeued twice, each time on a missing dependency (BL-3976, then BL-3990)

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261028-201954-L3.log:37`: BL-3981 claimed 3 times and requeued twice, each time on a missing dependency (BL-3976, then BL-3990).

## Evidence

Location: `logs/DarkFactory-20261028-201954-L3.log:37`

Requeue 1 at 2026-10-28 12:45:43: 'criterion 2 needs BL-3976's ManagedMultiTransfer wiring (cycle broken...)' (DarkFactory-20261028-111517-L4.log:70). Requeue 2 at 21:24:50: 'Criterion 2 needs the 10 ms timer removed from ManagedMultiTransfer: BL-3990' (DarkFactory-20261028-201954-L3.log:37). Done on the third claim on 2026-10-29. There was a dependency cycle between BL-3976 and BL-3981 that the board did not catch before the claim. The tool reports claims=3, cost $2.47.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3981 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3981 claims=3, 2 REQUEUE lines.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
