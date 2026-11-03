---
id: AF-0569
title: CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces.
key: process:logs:ci-run-41567287491:ci-red
reproduction: none
task: BL-4717
tasks: BL-4717
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_1017.md
---
# AF-0569 - CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261102-174304.log:74`: CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix.

## Evidence

Location: `logs/DarkFactory-20261102-174304.log:74`

Spell 2026-11-03T03:06:46Z to 03:41:55Z, 35.15 minutes, from run 41567287491 on 0afc799c (logs/ci-runs.json:573). It failed on stale known-failure lines for RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo and GoAway_LastStreamBeforeTheCall. Commit 8fa866b82 (BL-4594, 'drop known-failure lines the combined tree now passes') was made at 2026-11-02T19:46:28-07:00 (02:46:28Z), before the failing run ended. Runs 41568356821 (on 8fa866b8), 41568715899, 41569416558, 41569633497, 41570033273, 41570705387, 41570803068, 41570951688, 41571758205 and 41571869140 were all cancelled. 41570033273 was cancelled by the coordinator itself at 20:28:33 Arizona 'for lacking the CI fix BL-4604, BL-4605'. Green came only with 41572448811 at 03:41:55Z. The coordinator filed BL-4604 and BL-4605 at 19:55:19 Arizona, after the fix; L2 worked them one after the other (20:12:30, 20:23:01) and both closed with nothing to change ('already dropped by 8fa866b82'). This is the AF-0541 mechanism: red time after the fix, prolonged by cancelled runs.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41567287491; git log -1 --format='%cI %s' 8fa866b82; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-174304*.log -Pattern 'BL-460[45]|41567287491|41570033273'
```

- Expected: No CI red spell over 30 minutes, and no red time after a fix has landed.
- Actual: Spell 2026-11-03T03:06:46Z to 03:41:55Z, 35.15 minutes, runId 41567287491; the fix 8fa866b82 was made at 2026-11-02T19:46:28-07:00; BL-4604 and BL-4605 were filed at 19:55:19 and closed with nothing to change.

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | The given reproduction shows the 35.15-minute spell from 41567287491 and the fix 8fa866b82 at 2026-11-02T19:46:28-07:00. In this window part of the pattern recurs: fix 1df901115 (05:11 MST) had its run 41633887707 cancelled, and BL-4724/BL-4725 were filed at 05:20 for failures it had already fixed. But the red spell from 41632220793 (44.7 minutes) was mostly not post-fix: a QUIC regression from 62178b13 kept it red until fixes at 05:45-05:47 MST. CI was green 17 minutes after the last fix, with 2 runs cancelled. No spell spent more than 30 minutes red after its fix had landed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces.
