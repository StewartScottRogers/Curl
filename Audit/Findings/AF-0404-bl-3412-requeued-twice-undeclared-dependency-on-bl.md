---
id: AF-0404
title: BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3412:redone-work
reproduction: none
task: BL-3451
tasks: BL-3451
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0404 - BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261023-171352-L3.log:25`: BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split.

## Evidence

Location: `logs/DarkFactory-20261023-171352-L3.log:25`

Claim 1, L4 14:44:45 (opus, 25 turns, $0.73): REQUEUE 14:55:22 'Depends on BL-3410: both cases run runtests.pl ... criteria were template placeholders' (DarkFactory-20261023-110315-L4.log:237). Claim 2, L3 17:16:23 (opus retry, 47 turns, $2.19): 'stash uncommitted work kept' at 17:26:06, then REQUEUE 'Code done and Conformance tests green (stash); cost cap reached before the quality measurement and solution fast tests'. At 19:13 lanes L1, L2 and L3 all logged it 'unclaimable cost cap: 2.92 of its 3.00'. L1 split it into BL-3438 (land the stashed work, $1.25) and BL-3439. Cause: the task was ready with placeholder criteria and no declared dependency on BL-3410, so its first Opus run was wasted. The second run ran out of cap before the gates. Measured cost $2.92 (6.7x the median).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3412 (claim|REQUEUE|stash|split|unclaimable)'
```

- Expected: BL-3412 claimed once, after BL-3410 was done, and finished within its cap
- Actual: two claims, two REQUEUEs (dependency, then cost cap with a stash), three unclaimable lines and a split into BL-3438 and BL-3439

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
