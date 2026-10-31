---
id: AF-0474
title: BL-3837 (CI quality-gate fix) claimed 3 times: requeued on BL-3831, then Blocked on an unrelated Linux failure
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3837:redone-work
reproduction: none
task: BL-4191
tasks: BL-4191
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0474 - BL-3837 (CI quality-gate fix) claimed 3 times: requeued on BL-3831, then Blocked on an unrelated Linux failure

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261026-063535-L2.log:107`: BL-3837 (CI quality-gate fix) claimed 3 times: requeued on BL-3831, then Blocked on an unrelated Linux failure.

## Evidence

Location: `logs/DarkFactory-20261026-063535-L2.log:107`

The task Log shows 3 claims on 2026-10-26; the tool counts 2 (claims=2, $0.85). Requeue at 08:59:32: 'Waits on BL-3831, which covers WriteRefusalAsync's untaken branch' (DarkFactory-20261026-063535-L2.log:107). Then Doing -> Blocked because Build and test (ubuntu-latest) still failed on Format_OpenSslUnprintableNames (BL-3878's test), and Blocked -> Backlog once BL-3878 was Done. Claim 3 (DarkFactory-20261026-151632-L2.log:47, 16:37:42) finished it. A CI-fix task was filed while the task that fixed it (BL-3831) was already on the board.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3837 (claim|REQUEUE|BLOCKED|DONE)'; Select-String -Path Tasks/Done/2026-10-26/BL-3837-*.md -Pattern 'Backlog -> Doing'
```

- Expected: Fewer than 3 claims.
- Actual: 3 'Backlog -> Doing' lines in the task Log; claims at 08:54:54 (L2), and twice more in shift 20261026-151632.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | A CI fix was again requeued behind another task. BL-4083 (Fix flaky CI test job Quality gates windows-latest) was claimed by L1 at 07:21:03 and requeued at 07:24:44 ('Needs Curl.Engine.UnitLibrary/EngineComposition.cs, held whole by BL-4082'). It was reclaimed only at 10:46:11 and Done at 11:02:18, against ADR-1158. The Blocked part is not repeated: the only BLOCKED in the window is BL-4358, a timeout.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
