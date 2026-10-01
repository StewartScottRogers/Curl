---
id: AF-0004
title: CI on work/dark-factory was red for 90 minutes, as one spell over 30 minutes
auditor: process
severity: Low
status: proposed
key: process:logs:ci-work-dark-factory:ci-red
task: none
found: 2026-09-30
found-at: d065d6d3507e2ed87905d40a24233af193913378
scorecard: 2026-09-30_1754.md
closed:
closed-by:
---
# AF-0004 - CI on work/dark-factory was red for 90 minutes, as one spell over 30 minutes

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI on work/dark-factory was red for 90 minutes, as one spell over 30 minutes. Reported by an auditor flagged unreliable in 2026-09-30_1754.md.

## Evidence

Location: `logs/ci-runs.json`

ciRedMinutes=90. ci-runs.json shows a failed run created 2026-09-30T11:50Z and completed 12:00Z on head d065d6d3. The next successful run completed 13:30Z. This exceeds both the 60-minute total and the 30-minute single-spell thresholds. ciRunsFrom is 2026-09-30T11:50Z, later than -Since, so this covers less than the period. The failing test and the filing of a fix task were not traced; ci-whisper-*.json files in the log folder hold the notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-23 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedMinutes
```

- Expected: Total red time of 60 minutes or less and no spell over 30 minutes.
- Actual: 90

## Re-audits

