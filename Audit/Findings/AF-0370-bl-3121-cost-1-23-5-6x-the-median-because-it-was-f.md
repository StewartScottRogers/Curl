---
id: AF-0370
title: BL-3121 cost $1.23 (5.6x the median) because it was filed with touches naming only Curl.Networking.UnitLibrary, so its first Opus run found BL-3127 holding the test project and requeued
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-23_0618.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3223) are Done at 32b1dda228a5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3121:cost-outlier
reproduction: none
task: BL-3223
tasks: BL-3223
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed: 2026-10-23
closed-how: task-done-historical
closed-by: 2026-10-23_0618.md
---
# AF-0370 - BL-3121 cost $1.23 (5.6x the median) because it was filed with touches naming only Curl.Networking.UnitLibrary, so its first Opus run found BL-3127 holding the test project and requeued

## Summary

Low finding from the process auditor at `logs/BL-3121-20261022-143901-L2.jsonl`: BL-3121 cost $1.23 (5.6x the median) because it was filed with touches naming only Curl.Networking.UnitLibrary, so its first Opus run found BL-3127 holding the test project and requeued.

## Evidence

Location: `logs/BL-3121-20261022-143901-L2.jsonl`

Median task costUsd is 0.2207 (52 tasks), so the bar is 0.662. BL-3121 costUsd=1.2265, claims=2. Run 1 (L2, 14:39:44): 11 turns, $0.299, result: 'I moved BL-3121 back to Backlog without changing any code, because the file it needs is held by another lane.' Commit 8ea47c96c widened touches from [Curl.Networking.UnitLibrary] to [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]. BL-3127 (touches [Curl.Networking.UnitTests]) was claimed at 14:40:31 on L4. Run 2 (L1, 16:10:56, 'retry after a run that came back'): 28 turns, $0.927, 1.50M cache-read tokens. It included a refused Bash command ('Permission to use Bash with command cd /z/repos/C...') followed by mutating QuicDialer.cs by hand and reverting it. The fix was test-only, but routing chose Opus ('names HTTP/3').

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3121'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3121 claims=2 minutes=6.15 costUsd=1.2265 outcome=done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-23: accepted -> closed. Audit 2026-10-23_0618.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3223) are Done at 32b1dda228a5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
