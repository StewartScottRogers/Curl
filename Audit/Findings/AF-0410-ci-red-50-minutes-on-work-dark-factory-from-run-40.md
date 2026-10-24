---
id: AF-0410
title: CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS)
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:test1173:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0410 - CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T22:57:04Z to 23:47:24Z, 50.33 min, run 40034290028 on 93ed8666. The coordinator filed BL-3420 at 16:00:26 local, 3 min after the red (DarkFactory-20261023-110315.log:118). L3 claimed it at 16:04:05 and it was Done at 16:13:29 ('test1173's man page scan runs in linear time'). Run 40035852439 on 710327b2, pushed before the fix, also failed. The first green run, 40038429558, was created at 23:13:30Z and finished at 23:47Z.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40034290028
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T22:57:04Z end 23:47:24Z minutes 50.33 runId 40034290028

## Re-audits

## Log

- 2026-10-23: filed proposed.
