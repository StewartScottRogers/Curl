---
id: AF-0374
title: BL-3151 cost $0.96 (4.4x the median) across two Opus runs that each found the same unmet precondition and requeued
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3151:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0374 - BL-3151 cost $0.96 (4.4x the median) across two Opus runs that each found the same unmet precondition and requeued

## Summary

Low finding from the process auditor at `logs/BL-3151-20261022-164804-L1.jsonl`: BL-3151 cost $0.96 (4.4x the median) across two Opus runs that each found the same unmet precondition and requeued.

## Evidence

Location: `logs/BL-3151-20261022-164804-L1.jsonl`

BL-3151 costUsd=0.9619, against a bar of 0.662, and the task is not done. Run 1: 26 turns, $0.643, 980,161 cache-read tokens, 8,975 output tokens. Its time went on probing live pages and reporting that download/ and ai-help/ still return 404 until the shift-end merge. Run 2 ('retry after a run that came back'): 12 turns, $0.319, the same conclusion. The cause is the redone-work finding for BL-3151: a claim was allowed before the merge it depends on had landed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3151'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3151 claims=2 minutes=1.35 costUsd=0.9619 outcome=requeued

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
