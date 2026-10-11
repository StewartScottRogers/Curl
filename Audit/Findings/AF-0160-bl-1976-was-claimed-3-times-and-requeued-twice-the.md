---
id: AF-0160
title: BL-1976 was claimed 3 times and requeued twice, then blocked by the per-task cost cap; its fix sat in a stash until BL-2041 landed it
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-1976:BL-1976:redone-work
reproduction: none
task: BL-2053
tasks: BL-2053
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0160 - BL-1976 was claimed 3 times and requeued twice, then blocked by the per-task cost cap; its fix sat in a stash until BL-2041 landed it

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261010-181711-L1.log:6`: BL-1976 was claimed 3 times and requeued twice, then blocked by the per-task cost cap; its fix sat in a stash until BL-2041 landed it.

## Evidence

Location: `logs/DarkFactory-20261010-181711-L1.log:6`

Measure-FactoryProcess (-Since 2026-10-10) row: BL-1976 claims 3, costUsd 3.2467, outcome blocked. Claim 1 at 13:27:22 on L3 (DarkFactory-20261010-080057-L3.log:149): a 10-turn, 1.2 min run that only parked it ('REQUEUE ... Waits on BL-1997', line 156). Claim 2 at 16:17:57 on L1 (DarkFactory-20261010-161355-L1.log:11): 55 turns, 8 min, edits to TcpConnector.cs, then 'stash uncommitted work kept' at 16:26:03 (stash 252932ba6) and 'REQUEUE ... Waits on BL-2035' at 16:26:16. The task log says '--connect-to fix for FTP data connections left uncommitted'. Claim 3 at 18:19:53 (DarkFactory-20261010-181711-L1.log:6) hit 'cost cap its runs of the last day already cost 3.25 of its 3.93 US dollar cost cap' and BLOCKED at 18:20:04. The work was not lost: commit a42818f58 moved the stash to BL-2041, and b693876ad (21:39) has the stash's exact 6-file diffstat. Cause: the lane claimed a task whose prerequisites (BL-1997, then a harness change) were not Done, so the runs found the dependency instead of doing the work. Severity is Low because the waste (about 3.25 USD and about 10 lane-minutes) is far below 20% of lane time.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-*-L*.log -Pattern 'BL-1976 (claim|REQUEUE|BLOCKED|cost cap)'
```

- Expected: BL-1976 is claimed once and finished in that run, with no REQUEUE line.
- Actual: 3 claim lines (13:27:22 L3, 16:17:57 L1, 18:19:53 L1), 2 REQUEUE lines (13:36:41, 16:26:16), a 'cost cap' line and BLOCKED at 18:20:04.

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
