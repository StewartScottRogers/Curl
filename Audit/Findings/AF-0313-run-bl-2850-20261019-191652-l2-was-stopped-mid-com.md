---
id: AF-0313
title: Run BL-2850-20261019-191652-L2 was stopped mid-command when shift 191652 was replaced after 7 minutes, but the tool lists it as running, not unfinished
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-2850:unfinished-run
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0313 - Run BL-2850-20261019-191652-L2 was stopped mid-command when shift 191652 was replaced after 7 minutes, but the tool lists it as running, not unfinished

## Summary

Low finding from the process auditor at `logs/BL-2850-20261019-191652-L2.jsonl`: Run BL-2850-20261019-191652-L2 was stopped mid-command when shift 191652 was replaced after 7 minutes, but the tool lists it as running, not unfinished.

## Evidence

Location: `logs/BL-2850-20261019-191652-L2.jsonl`

runningRuns = [BL-2850-20261019-191652-L2.jsonl]; task row BL-2850 outcome open, 134.82 min, costUsd 0. The run's last lines are a Bash tool_use ('Requeue BL-2850 and commit board': split into BL-2863 to BL-2866, then task-board.ps1 move -To Backlog and git commit) and a 30-second heartbeat. There is no result. The file was last written at 19:23. Shift 191652 started 9 lanes at 19:16:58 and its log has no 'shift end' line. Shift 192417 started at 19:24:24 and adopted all 9 of its lanes ('lane 2 adopts BL-2850 from the previous shift, work in place', 192417.log:7). Its L2 integrated the stopped lane's work at 19:24:53. So 191652 was stopped, not live, at the copy at 21:41: every later shift (192417, 211325, 212549) logs 'shift end'. What stopped it: the shift was replaced at 19:24 (a restart). 8 other runs of that shift were resumed (*-192417-*-resumed.jsonl). It was recovered by adoption. Test-ShiftLive (Audit/Tools/Measure-FactoryProcess.ps1:128) counts any shift log without 'shift end' as live, so a stopped shift's unfinished run is listed under runningRuns instead of unfinishedRuns.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).runningRuns; Select-String -Path Z:\repos\Curl.audit\20261019-213437\logs\DarkFactory-20261019-192417.log -Pattern 'adopts BL-2850'
```

- Expected: runningRuns empty (no shift was live at the copy); the stopped run is not left unaccounted.
- Actual: runningRuns: BL-2850-20261019-191652-L2.jsonl, while 192417.log:7 says 'lane 2 adopts BL-2850 from the previous shift, work in place'.

## Re-audits

## Log

- 2026-10-19: filed proposed.
