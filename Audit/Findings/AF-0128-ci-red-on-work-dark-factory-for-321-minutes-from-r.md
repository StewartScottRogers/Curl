---
id: AF-0128
title: CI red on work/dark-factory for 321 minutes from run 37733074018 on 711fbdf0
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:37733074018:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0128 - CI red on work/dark-factory for 321 minutes from run 37733074018 on 711fbdf0

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red on work/dark-factory for 321 minutes from run 37733074018 on 711fbdf0.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-08T05:41:00Z, end 2026-10-08T11:01:51Z, minutes 320.85, runId 37733074018, stillRed false. ci-runs.json: 37733074018 failure on 711fbdf0 (05:34:39Z), 37733666115 failure on f06bf08c (05:41:46Z), then no CI run at all for five hours, then eight cancelled runs from 10:44Z to 10:51Z (f4967c9f to bede0122), then 37766645992 success on 3e8da37c (10:55Z). The logs are local time, UTC-7 (6687000's CI ran at 18:07Z and its merge is logged at 11:13). So the spell ran overnight (22:41 to 04:01 local), when no shift pushed. The fix commit came from a shift whose logs are not in the copied folder. The next master merge (6687000, pull request #74, DarkFactory-20261008-082227.log:50) went in only after its own green CI runs, so there was no merge with CI red, which keeps this at Low.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-08 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells
```

- Expected: No red spell over 30 minutes.
- Actual: One spell: 2026-10-08T05:41:00Z to 2026-10-08T11:01:51Z, 320.85 minutes, runId 37733074018.

## Re-audits

## Log

- 2026-10-08: filed proposed.
