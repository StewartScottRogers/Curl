---
id: AF-0500
title: BL-4328 requeued twice (CurlComposition.cs held by BL-4320, then the 80-turn cap) and then Deferred and split
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-4328:BL-4328:redone-work
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
# AF-0500 - BL-4328 requeued twice (CurlComposition.cs held by BL-4320, then the 80-turn cap) and then Deferred and split

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261029-184521-L7.log`: BL-4328 requeued twice (CurlComposition.cs held by BL-4320, then the 80-turn cap) and then Deferred and split.

## Evidence

Location: `logs/DarkFactory-20261029-184521-L7.log`

L14 claimed it at 22:06:44. That run took 22 turns and 4.7 min, and was requeued at 22:16:33 ('Needs Curl.Console/CurlComposition.cs, held by BL-4320 in Doing'). L7 claimed it at 22:25:11. That run took 81 turns and 50.5 min, and was requeued at 00:07:54 ('Dark factory stopped at its 80-turn cap'). L2 split it over the cost cap at 13:02:42 on 10-30 into BL-4362..BL-4365, and it was Deferred. The file conflict was found only after a claim and a run, not at claim time.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4328 (claim|end|REQUEUE|split)'
```

- Expected: A held file is seen at claim time; a too-big task is split before an 80-turn run.
- Actual: 2 REQUEUE (held file; 80-turn cap), then split and Deferred.

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
