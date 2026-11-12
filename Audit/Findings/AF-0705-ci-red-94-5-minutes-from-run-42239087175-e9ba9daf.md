---
id: AF-0705
title: CI red 94.5 minutes from run 42239087175 (e9ba9daf, SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout)
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:run-42239087175:ci-red
reproduction: none
task: none
tasks:
found: 2026-11-11
found-at: df2273e7663bedd3179f7d3166167d9ce99c6ca0
scorecard: 2026-11-11_2002.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0705 - CI red 94.5 minutes from run 42239087175 (e9ba9daf, SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 94.5 minutes from run 42239087175 (e9ba9daf, SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-06T15:19:48Z to 16:54:19Z, 94.52 minutes, run 42239087175 on e9ba9daf. The lane logs name test SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-05 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No spell over 30 minutes
- Actual: A 94.52-minute spell at run 42239087175

## Re-audits

## Log

- 2026-11-11: filed proposed.
