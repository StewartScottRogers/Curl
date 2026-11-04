---
id: AF-0625
title: CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI
auditor: process
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: process:logs:41688300433:ci-red
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0625 - CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261103-134646.log:17`: CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI.

## Evidence

Location: `logs/DarkFactory-20261103-134646.log:17`

ciRedSpells: 2026-11-03T18:20:55Z to 20:23:53Z (11:20 to 13:23 Arizona), 122.97 min, runId 41688300433 on 28cbd2df, an interactive commit 'fix(factory): cancel a running CI run for lacking a fix only once it has failed'. The 080214 coordinator was idle and polling, with no ci lines from 08:26 to 13:46. The next shift's coordinator read the run at 13:51:04 and logged 'cannot read its log with gh run view --log-failed' (134646.log:17; again at 145254.log:31), so no task was filed. The spell ended on run 41691198001 for ac11d86f, which itself took 133 minutes (18:10Z to 20:23Z). The failing test is unknown because the coordinator could not read the log.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41688300433; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-*.log -Pattern '41688300433'
```

- Expected: No red spell over 30 minutes; the failing test filed as a CI fix task
- Actual: Spell 122.97 min; coordinator lines say only 'cannot read its log with gh run view --log-failed'; no task filed

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
