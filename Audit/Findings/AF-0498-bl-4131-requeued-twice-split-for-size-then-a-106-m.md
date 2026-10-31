---
id: AF-0498
title: BL-4131 requeued twice (split for size, then a 106-minute 'shift deadline' run) and then Deferred and split again
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:BL-4131:BL-4131:redone-work
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
# AF-0498 - BL-4131 requeued twice (split for size, then a 106-minute 'shift deadline' run) and then Deferred and split again

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261030-060635-L13.log`: BL-4131 requeued twice (split for size, then a 106-minute 'shift deadline' run) and then Deferred and split again.

## Evidence

Location: `logs/DarkFactory-20261030-060635-L13.log`

L3 claimed it at 18:10:25 on 10-29 and requeued it at 18:56:56 ('Split for size: waits on BL-4303, BL-4304, BL-4305'). L13 claimed it at 06:18:52 on 10-30. That run took 67 turns, $4.15 and 106 min, and was requeued at 08:09:38: 'Shift deadline: code done and 99 tests pass, but the fast tests and a WebDavTools quality re-run ... are still to do'. Its result record says the code was 'uncommitted for the shift to stash'. L1 then split it over the cost cap at 15:41:38 into BL-4373 and BL-4374, and it was Deferred. That made two separate splits over two runs. Task cost $4.93 with no Done.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4131 (claim|end|REQUEUE|split)'
```

- Expected: A task split once, early; a near-finished run commits its work.
- Actual: 2 REQUEUE (split for size; shift deadline after 105.6 min with the code uncommitted), then a second split and Deferred.

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: requeues 0, tasksClaimedMoreThanOnce 0. No shift-deadline requeue and no task split twice.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
