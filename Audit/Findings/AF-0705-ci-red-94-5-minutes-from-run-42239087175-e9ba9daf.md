---
id: AF-0705
title: CI red 94.5 minutes from run 42239087175 (e9ba9daf, SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout)
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces.
key: process:logs:run-42239087175:ci-red
reproduction: none
task: BL-5002
tasks: BL-5002
found: 2026-11-11
found-at: df2273e7663bedd3179f7d3166167d9ce99c6ca0
scorecard: 2026-11-11_2002.md
duplicate-of:
closed: 2026-11-12
closed-how: reliable-reaudit
closed-by: 2026-11-12_2105.md
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

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Window shows no SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout failure in the ci lines; 58 tasks done.

## Log

- 2026-11-12: accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-11: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces.
