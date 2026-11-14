---
id: AF-0707
title: CI red 74 minutes from run 43110153266 (27be8cbc, Quality gates Measure coverage)
auditor: process
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: process:logs:run-43110153266:ci-red
reproduction: none
task: BL-5176
tasks: BL-5004, BL-5176
found: 2026-11-11
found-at: df2273e7663bedd3179f7d3166167d9ce99c6ca0
scorecard: 2026-11-11_2002.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0707 - CI red 74 minutes from run 43110153266 (27be8cbc, Quality gates Measure coverage)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 74 minutes from run 43110153266 (27be8cbc, Quality gates Measure coverage).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-11T22:34:57Z to 23:48:57Z, 74 minutes, run 43110153266 on 27be8cbc. The 101854 and 190859 logs repeat the same Quality gates job failure from 15:36 to 16:22 local.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-05 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No spell over 30 minutes
- Actual: A 74-minute spell at run 43110153266

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | reproduces: yes | ciRedSpells over the window shows run 43110153266 as a 74 min spell, over 30; the Quality gates 'Measure coverage' cause recurred in spell 7.
- 2026-11-13 | 2026-11-13_2321.md | reproduces: yes | Ran the reproduction with -Since 2026-11-05: run 43110153266 is still in ciRedSpells at 74 minutes. The window also holds new spells over 30 minutes without a filing trace.

## Log

- 2026-11-12: accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-11: filed proposed.
