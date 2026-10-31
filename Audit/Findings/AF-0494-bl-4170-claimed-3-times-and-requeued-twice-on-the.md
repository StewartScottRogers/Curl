---
id: AF-0494
title: BL-4170 claimed 3 times and requeued twice on the run budget with work stashed; its split's push was refused
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-4170:BL-4170:redone-work
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
# AF-0494 - BL-4170 claimed 3 times and requeued twice on the run budget with work stashed; its split's push was refused

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261029-140509-L1.log`: BL-4170 claimed 3 times and requeued twice on the run budget with work stashed; its split's push was refused.

## Evidence

Location: `logs/DarkFactory-20261029-140509-L1.log`

Claims: L5 09:18:58, which ended after 16 turns and 5.5 min and was requeued at 09:24:52 ('Run budget spent after the tokenizer (left uncommitted for the shift's stash)'); L1 14:16:16, which ended after 21 turns and 8.1 min and was requeued at 14:29:53 ('Run budget spent after the parser and descriptors (uncommitted for the shift's stash)'). Then L10 at 14:11:51 logged 'split over its cost cap; one split run' and at 14:31:39 'failed: the push of the split was refused; blocked'. The task file records Backlog -> Blocked ('cost cap ... split the task; the automatic split failed: the push of the split was refused'), then Blocked -> Backlog when the cost-cap window passed. A third claim, L11 at 20:26:54, ran 39 turns and was Done at 20:51:26. That breaks ADR-1155's two-claim limit. Total cost $4.80 over three claims for a task one 39-turn run finished.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4170 (claim|end|REQUEUE|DONE|split)'
```

- Expected: At most two claims; a failed split does not fall back to a third full claim.
- Actual: 3 claims, 2 REQUEUE (run budget, work stashed), a failed split push, then a third claim that finished.

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
