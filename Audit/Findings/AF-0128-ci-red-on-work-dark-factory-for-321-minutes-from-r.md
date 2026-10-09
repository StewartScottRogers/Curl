---
id: AF-0128
title: CI red on work/dark-factory for 321 minutes from run 37733074018 on 711fbdf0
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:37733074018:ci-red
reproduction: none
task: BL-1878
tasks: BL-1878
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

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | still reported
- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the reproduction (-Since 2026-10-08): ciRedSpells lists 2026-10-08T05:41:00Z to 11:01:51Z, 320.85 minutes, runId 37733074018. It is the same incident, still inside a window that starts at a date. The only other spell, 37907205689, lasted 11.52 minutes.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: yes | Ran the reproduction (-Since 2026-10-08). ciRedSpells: 37733074018 320.85 min (the original incident, 711fbdf0), 94826414012 90 min (rests on impossible CI runs, reported as a separate finding), 37907205689 11.52 min, and 37931431648 30.42 min (2026-10-09T12:45:17Z to 13:15:42Z on ea846a2e). By mechanism over this window, CI on work/dark-factory again stayed red for over 30 minutes in one spell: run 37931431648, caused by lane-1 task BL-1876's edit to the guard file task-board.ps1 in shift 20261009-050349. The spell is much shorter than the original 321 minutes, but it still crosses the rule.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
