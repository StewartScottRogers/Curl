---
id: AF-0414
title: BL-3416 cost $2.69 (6.1x the median) in one 41-turn Opus run finishing the checksrc.pl entry point
auditor: process
severity: Low
status: rejected
reason: Stewart rejected it on 2026-10-24: a $2.69 run that finished its task in one run is not waste; cost outliers now also need a run over $5 (as AF-0415).
key: process:BL-3416:cost-outlier
reproduction: none
task: BL-3461
tasks: BL-3461
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0414 - BL-3416 cost $2.69 (6.1x the median) in one 41-turn Opus run finishing the checksrc.pl entry point

## Summary

Low finding from the process auditor at `logs/BL-3416-20261023-110315-L3.jsonl`: BL-3416 cost $2.69 (6.1x the median) in one 41-turn Opus run finishing the checksrc.pl entry point.

## Evidence

Location: `logs/BL-3416-20261023-110315-L3.jsonl`

One run: opus feature pipeline, 41 turns, 16.9 min, $2.69, 3.69M cache-read tokens, 47.2k output tokens. It started by applying an earlier run's stash c1205b3f at 15:11:42. Done at 15:30:50. Cost driver: Opus output-heavy porting work (47k output tokens) on a stashed continuation.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-20261023-110315-L3.log -Pattern 'BL-3416 (claim|stash|end|DONE)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3416 costUsd 2.6877; '41 turns, 16.9 min [model opus: feature pipeline]'

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> rejected.
