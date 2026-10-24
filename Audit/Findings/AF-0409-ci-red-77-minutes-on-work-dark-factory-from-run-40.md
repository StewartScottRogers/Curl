---
id: AF-0409
title: CI red 77 minutes on work/dark-factory from run 40021720442 (Conversation_AnswersAsRecorded on macOS); 23 runs cancelled before the first green
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:Conversation_AnswersAsRecorded:ci-red
reproduction: none
task: BL-3456
tasks: BL-3456
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0409 - CI red 77 minutes on work/dark-factory from run 40021720442 (Conversation_AnswersAsRecorded on macOS); 23 runs cancelled before the first green

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 77 minutes on work/dark-factory from run 40021720442 (Conversation_AnswersAsRecorded on macOS); 23 runs cancelled before the first green.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T21:02:11Z to 22:19:09Z, 76.97 min, run 40021720442 on 2640c8ca, macOS only. The coordinator waited for a second platform's result, then filed BL-3415 at 14:09:25 local, 7 min after the red (DarkFactory-20261023-110315.log:82-86). L1 claimed it at 14:22:28 and it was Done at 14:29:59. Runs 40023582861 (989333e7) and 40026750438 (fff2f3b1), both created before the fix, failed as well. Between 20:45Z and 21:51Z, ci-runs.json shows 23 cancelled runs. The first run that finished after the fix, 40029783223, was created at 21:51:49Z, so the spell lasted about 50 min longer than the fix took.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40021720442
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T21:02:11Z end 22:19:09Z minutes 76.97 runId 40021720442

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
