---
id: BL-4717
title: Fix AF-0569: CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4717 — Fix AF-0569: CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix

## Goal

The cause of the factory behaviour the audit office reported as AF-0569 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0569 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0569-ci-red-35-15-minutes-from-run-41567287491-0afc799c.md`.

Location: `logs/DarkFactory-20261102-174304.log:74`

Location: `logs/DarkFactory-20261102-174304.log:74`

Spell 2026-11-03T03:06:46Z to 03:41:55Z, 35.15 minutes, from run 41567287491 on 0afc799c (logs/ci-runs.json:573). It failed on stale known-failure lines for RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo and GoAway_LastStreamBeforeTheCall. Commit 8fa866b82 (BL-4594, 'drop known-failure lines the combined tree now passes') was made at 2026-11-02T19:46:28-07:00 (02:46:28Z), before the failing run ended. Runs 41568356821 (on 8fa866b8), 41568715899, 41569416558, 41569633497, 41570033273, 41570705387, 41570803068, 41570951688, 41571758205 and 41571869140 were all cancelled. 41570033273 was cancelled by the coordinator itself at 20:28:33 Arizona 'for lacking the CI fix BL-4604, BL-4605'. Green came only with 41572448811 at 03:41:55Z. The coordinator filed BL-4604 and BL-4605 at 19:55:19 Arizona, after the fix; L2 worked them one after the other (20:12:30, 20:23:01) and both closed with nothing to change ('already dropped by 8fa866b82'). This is the AF-0541 mechanism: red time after the fix, prolonged by cancelled runs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41567287491; git log -1 --format='%cI %s' 8fa866b82; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-174304*.log -Pattern 'BL-460[45]|41567287491|41570033273'
```

- Expected: No CI red spell over 30 minutes, and no red time after a fix has landed.
- Actual: Spell 2026-11-03T03:06:46Z to 03:41:55Z, 35.15 minutes, runId 41567287491; the fix 8fa866b82 was made at 2026-11-02T19:46:28-07:00; BL-4604 and BL-4605 were filed at 19:55:19 and closed with nothing to change.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
