---
id: AF-0495
title: BL-4124 claimed 3 times: split for size, then a lane-12 rebase conflict the resolver could not settle
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:BL-4124:BL-4124:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0495 - BL-4124 claimed 3 times: split for size, then a lane-12 rebase conflict the resolver could not settle

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261029-140509-L12.log`: BL-4124 claimed 3 times: split for size, then a lane-12 rebase conflict the resolver could not settle.

## Evidence

Location: `logs/DarkFactory-20261029-140509-L12.log`

L1 claimed it at 10:17:14 and requeued it at 10:20:59 ('Split for size: waits on BL-4197, BL-4198, BL-4199'). L12 claimed it at 14:14:18 and ran four runs (60 turns/44 min, then 10/6.9, 10/5.9 and 12/8.8 min). At 16:08:23 it was requeued: 'Lane 12 could not integrate: rebase conflict the resolver could not settle. The work is on branch factory/BL-4124-lane-12-20261029-140509'. L11 claimed it a third time at 16:09:54 and ran four more runs (14 turns/37.9 min, 13/11.4, 16/8.8, 24/6.2) before DONE at 18:17:45. Total 127.85 min and $4.81. A third claim, despite ADR-1155.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4124 (claim|end|REQUEUE|DONE)'
```

- Expected: A task claimed at most twice; an integration conflict resolved without a fresh claim.
- Actual: 3 claims, REQUEUE on split for size and on an unresolved rebase conflict.

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: requeues 0. The one rebase conflict (L1, BL-4385, 06:13:49) was settled by a 6-turn, 0.6-minute resolve run, and the task reached Done at 06:15:22 in one claim.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
