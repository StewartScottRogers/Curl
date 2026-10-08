---
id: AF-0051
title: BL-1467 claimed 3 times and requeued twice: lanes 4 and 5 each failed to integrate after rebasing onto other lanes' work
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-1467:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0051 - BL-1467 claimed 3 times and requeued twice: lanes 4 and 5 each failed to integrate after rebasing onto other lanes' work

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261006-200306-L4.log:42`: BL-1467 claimed 3 times and requeued twice: lanes 4 and 5 each failed to integrate after rebasing onto other lanes' work.

## Evidence

Location: `logs/DarkFactory-20261006-200306-L4.log:42`

Lane 4 claimed BL-1467 at 06:36:49 (commit b0b89735e) and finished at 06:42:51 (40 turns, 5.9 min). At 06:45:57 it logged 'PARKED build failed after rebasing onto the other lanes' work' and REQUEUE (606d11f9c). Lanes 1, 3 and 6 integrated BL-1465, BL-1528 and BL-1455 between 06:40:57 and 06:45:39. Lane 5 claimed it at 06:49:10 (L5.log:62) and ran only 1.9 min (21 turns): build ok, test ok, move Done, with no fix for the integration break. It was parked again at 06:54:30 (L5.log:70, 365389a10). Lane 4's third claim at 07:00:50 fixed the build: 'build FAIL 4 error(s)' at 07:02:14, then ok, and DONE at 07:09:01 (f42b1420c). The errors in BL-1467-20261006-200306-L4.jsonl are 'CS0103: The name DiagnosticAssertionLines does not exist' and 'CS0407: string CredentialCacheStore.DefaultCacheName() / KeytabStore.DefaultKeytabName() has the wrong return type'. These are conflicts with work integrated in parallel. The second run repeated the in-lane check that had already passed instead of reproducing the integration failure, so it was wasted. The task's own Log confirms both requeues.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-03 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1467' | Select-Object id,claims,outcome; Select-String -Path ..\logs\DarkFactory-20261006-200306-L4.log,..\logs\DarkFactory-20261006-200306-L5.log -Pattern 'BL-1467 (claim|PARKED|REQUEUE|DONE)'
```

- Expected: BL-1467 claims 1: one claim and one DONE.
- Actual: BL-1467 claims 3, outcome done. Claims at 06:36:49 (L4), 06:49:10 (L5) and 07:00:50 (L4); PARKED/REQUEUE at 06:45:57 and 06:54:30; DONE at 07:09:01.

## Re-audits

- 2026-10-07 | 2026-10-07_1336.md | not re-audited | Ran the reproduction. Measure-FactoryProcess.ps1 -Since 2026-10-03 has no task BL-1467: the log folder holds only shift 20261007-111121. Select-String failed with 'Cannot find path ...\logs\DarkFactory-20261006-200306-L4.log because it does not exist' (the L5 log is missing too). The result cannot tell; not re-audited.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
