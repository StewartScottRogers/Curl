---
id: AF-0004
title: 31 tasks cost over 3x the median task cost; the costliest are BL-703, BL-527, BL-568, BL-658 and BL-708
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-02_1400.md: the reproduction no longer reproduces.
key: process:logs:task-costs:cost-outlier
task: BL-1070
found: 2026-09-30
found-at: d065d6d3507e2ed87905d40a24233af193913378
scorecard: 2026-09-30_1754.md
closed: 2026-10-02
closed-by: 2026-10-02_1400.md
---
# AF-0004 - 31 tasks cost over 3x the median task cost; the costliest are BL-703, BL-527, BL-568, BL-658 and BL-708

## Summary

Low finding from the process auditor at `logs/BL-703-*.jsonl`: 31 tasks cost over 3x the median task cost; the costliest are BL-703, BL-527, BL-568, BL-658 and BL-708.

## Evidence

Location: `logs/BL-703-*.jsonl`

The median cost of done tasks is about $2.36, so the 3x bar is about $7.08. The five costliest tasks are BL-703 ($14.33, 36 minutes), BL-527 ($13.83, 40 minutes), BL-568 ($12.95, 57 minutes), BL-658 ($12.58, 31 minutes) and BL-708 ($11.93, claimed twice, 27 minutes). Total cost is $1524.54, or $3.01 per task done. The causes (turns, retries, rereading) were not traced in the run logs.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5 id,costUsd
```

- Expected: No task over 3 times the median cost.
- Actual: BL-703 14.33, BL-527 13.83, BL-568 12.95, BL-658 12.58, BL-708 11.93, against a median of about 2.36.

## Re-audits

- 2026-10-02 | 2026-10-02_1400.md | reproduces: no | The five costliest tasks are BL-1105 8.52, BL-710 7.29, BL-942 7.12, BL-988 6.80 and BL-1049 6.74 USD. BL-703, BL-527, BL-568, BL-658 and BL-708 are absent. 17 tasks are over 3x the median of 1.67 USD, not 31.

## Log

- 2026-09-30: filed proposed.
- 2026-09-30: proposed -> accepted. Stewart accepted it on 2026-09-30 (log written 2026-10-02, BL-1183).
- 2026-10-02: accepted -> closed. Re-audit 2026-10-02_1400.md: the reproduction no longer reproduces.
