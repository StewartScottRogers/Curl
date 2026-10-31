---
id: AF-0497
title: BL-4137 claimed 3 times: split for size, then a 106-minute, 83-turn run that ran out of time with its code stashed
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-4137:BL-4137:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0497 - BL-4137 claimed 3 times: split for size, then a 106-minute, 83-turn run that ran out of time with its code stashed

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261030-060635-L6.log`: BL-4137 claimed 3 times: split for size, then a 106-minute, 83-turn run that ran out of time with its code stashed.

## Evidence

Location: `logs/DarkFactory-20261030-060635-L6.log`

L5 claimed it at 19:26:31 on 10-29 and requeued it at 20:00:08 ('Split for size into BL-4311, BL-4309 and BL-4310'). L6 claimed it at 06:25:08 on 10-30. That run (BL-4137-20261030-060635-L6.jsonl) took 83 turns, $3.91 and 106 min, and was requeued at 08:11:56: 'Out of time: code and tests written (uncommitted, stashed)'. L4 claimed it a third time at 08:13:00, finished in 35 turns, 26.6 min and $0.99, and was Done at 08:44:30. Total $5.65. The same 08:09-08:11 'out of time' requeue hit BL-4131 (L13) and BL-4132 (L4) as well: all three runs were about 105 min long.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4137 (claim|end|REQUEUE|DONE)'
```

- Expected: At most two claims.
- Actual: 3 claims, 2 REQUEUE (split for size; out of time after 105.9 min with the code stashed).

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
