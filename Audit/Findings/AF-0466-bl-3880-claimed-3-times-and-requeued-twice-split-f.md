---
id: AF-0466
title: BL-3880 claimed 3 times and requeued twice (split for size, then a file held by another task)
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3880:redone-work
reproduction: none
task: BL-4184
tasks: BL-4184
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0466 - BL-3880 claimed 3 times and requeued twice (split for size, then a file held by another task)

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261028-111517-L4.log:27`: BL-3880 claimed 3 times and requeued twice (split for size, then a file held by another task).

## Evidence

Location: `logs/DarkFactory-20261028-111517-L4.log:27`

The task Log shows 3 claims on 2026-10-28. Requeue 1 at 09:42:20: 'Split for size: waits on BL-3948 ... BL-3952' (DarkFactory-20261028-052757-L4.log:137). Requeue 2 at 11:52:36: 'Waits on BL-3975 (-m writes its timeout line twice; HttpProtocolHandler.cs, held by BL-3874)' (DarkFactory-20261028-111517-L4.log:27). Claim 3 finished Done. The tool reports claims=3, cost $2.39.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3880 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3880 claims=3, 2 REQUEUE lines.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | Split-for-size requeues were again followed by a second requeue and a third claim. BL-4124: split for size at 10:20:59, then 'Lane 12 could not integrate: rebase conflict' at 16:08:23, third claim L11 at 16:09:54. BL-4132: split for size at 18:54:56, then 'Out of time' at 08:10:29, third claim at 08:11:06. BL-4137: split at 20:00:08, then 'Out of time' at 08:11:56, third claim at 08:13:00.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
