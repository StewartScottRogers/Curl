---
id: AF-0496
title: BL-4132 claimed 3 times: split for size, then a 105-minute run that ran out of time with its fix stashed
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:BL-4132:BL-4132:redone-work
reproduction: none
task: BL-4406
tasks: BL-4406
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
---
# AF-0496 - BL-4132 claimed 3 times: split for size, then a 105-minute run that ran out of time with its fix stashed

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261030-060635-L4.log`: BL-4132 claimed 3 times: split for size, then a 105-minute run that ran out of time with its fix stashed.

## Evidence

Location: `logs/DarkFactory-20261030-060635-L4.log`

L11 claimed it at 18:18:09 on 10-29 and requeued it at 18:54:56 ('Split for size: waits on BL-4302, BL-4300, BL-4301'). L4 claimed it at 06:24:02 on 10-30. That run ended after 70 turns and 105.2 min, and was requeued at 08:10:29: 'Out of time: hook and option built and fixed in the stash; needs one CompressionDictionary-only quality run, the fast tests and the commit'. Its result record says 'I ran out of time ... left uncommitted for the shift to stash'. L2 claimed it a third time at 08:11:06, finished in 15 turns and 13.5 min, and was Done at 08:32:18. The 105-min run did nearly all the work but could not commit it, so a third claim, against ADR-1155, finished it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4132 (claim|end|REQUEUE|DONE)'
```

- Expected: At most two claims; a run that has built and tested the change commits it rather than stashing it.
- Actual: 3 claims; REQUEUE 'Split for size' then 'Out of time ... in the stash' after 105.2 min.

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: requeues 0, tasksClaimedMoreThanOnce 0. No run ran out of time with its fix stashed. The longest run (BL-4389, 72.6 min) ended Done.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
