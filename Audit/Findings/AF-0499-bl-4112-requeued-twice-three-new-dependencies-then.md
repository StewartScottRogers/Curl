---
id: AF-0499
title: BL-4112 requeued twice (three new dependencies, then the 80-turn cap at $5.10) and then Deferred and split
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: process:BL-4112:BL-4112:redone-work
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
# AF-0499 - BL-4112 requeued twice (three new dependencies, then the 80-turn cap at $5.10) and then Deferred and split

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261029-140509-L5.log`: BL-4112 requeued twice (three new dependencies, then the 80-turn cap at $5.10) and then Deferred and split.

## Evidence

Location: `logs/DarkFactory-20261029-140509-L5.log`

L3 claimed it at 10:12:12 and requeued it at 10:18:11 ('Waits on BL-4196 (shared WebSocket codec), BL-4194 and BL-4195'). L5 claimed it at 15:16:55. That run took 81 turns, 54.7 min and $5.10, and was requeued at 16:25:53: 'Dark factory stopped at its 80-turn cap, so the task is too big for one run'. The split run split-BL-4112-20261029-164929-L7.jsonl (14 turns, $0.21) split it into BL-4294..BL-4297, and it was Deferred ('cost cap exceeded'). $5.81 was spent with no Done; the 80-turn run produced nothing that integrated.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4112 (claim|end|REQUEUE|split)'
```

- Expected: A task too big for one run is split before a full 80-turn run is spent on it.
- Actual: 2 REQUEUE (new dependencies; 80-turn cap), then split and Deferred.

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | By mechanism over this window: requeues 0. No run hit the 80-turn cap (maximum 40 turns).

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
