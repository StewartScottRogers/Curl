---
id: AF-0406
title: BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap
auditor: process
severity: Medium
status: closed
reason: Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces.
key: process:logs:BL-3234:redone-work
reproduction: none
task: BL-3453
tasks: BL-3453
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed: 2026-10-24
closed-how: reliable-reaudit
closed-by: 2026-10-24_0845.md
---
# AF-0406 - BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261023-110315-L1.log:11`: BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap.

## Evidence

Location: `logs/DarkFactory-20261023-110315-L1.log:11`

Claim 1, L3 04:41:42 (opus feature, 28 turns): REQUEUE 04:46:47 'Waiting on BL-3284 to BL-3288 (the protocol and proxy server ports, split out for size) and BL-3235'. Claim 2, L1 11:04:17 (opus retry, 24 turns): REQUEUE 11:08:44 'Waiting on BL-3334 (the SOCKS4/SOCKS5 half of proxy_server.py)'. At 17:14:44 L3 logged it 'unclaimable cost cap 1.97 of 3.07' and split it into BL-3424..BL-3427 at 17:16:23. Both Opus runs did nothing but find that a prerequisite task was not done. The board offered it as ready without the dependency declared.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3234 (claim|REQUEUE|split|unclaimable)'
```

- Expected: BL-3234 not claimable until BL-3284..3288, BL-3235 and BL-3334 were done
- Actual: two claims each ending in a REQUEUE naming an unfinished prerequisite, then unclaimable and split

## Re-audits

- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | Re-audited by mechanism over this window (13 tasks Done). No task was requeued while waiting on unfinished prerequisite tasks, and none was split over its cost cap. The REQUEUE lines are only BL-3444's (overlap) and BL-3447's ('Lane restarted.').

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces.
