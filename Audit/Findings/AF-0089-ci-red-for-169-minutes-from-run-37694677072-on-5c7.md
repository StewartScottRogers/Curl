---
id: AF-0089
title: CI red for 169 minutes from run 37694677072 on 5c771af7; the fix was pushed at 23:03Z but every CI run was cancelled for two hours
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:CI-37694677072:ci-red
reproduction: none
task: BL-1767
tasks: BL-1767
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0089 - CI red for 169 minutes from run 37694677072 on 5c771af7; the fix was pushed at 23:03Z but every CI run was cancelled for two hours

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261007-111121.log:75`: CI red for 169 minutes from run 37694677072 on 5c771af7; the fix was pushed at 23:03Z but every CI run was cancelled for two hours.

## Evidence

Location: `logs/DarkFactory-20261007-111121.log:75`

ciRedSpells[0]: start 2026-10-07T22:19:58Z, end 2026-10-08T01:09:09Z, 169.18 minutes, run 37694677072. The coordinator log shows: 15:21:46 BL-1650 filed (Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNume~); 15:21:47 BL-1651 filed (EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother); 15:21:47 run 37694677072 failure on 5c771af7 (the 'claim BL-1627 on lane 7' commit). That is 2 minutes from red to filed. Further failures: 37695687015 on e0a1b8b1 and 37698759385 on 6677cf92. BL-1650 was integrated at 15:43:42 (lane 8) and BL-1651 at 16:02:59 (lane 9), local time -0700 = 23:02:59Z. CI did not report green until 01:09:09Z (run 37710969213 on cc91b0c9): all 115 CI runs created between 22:58Z and 01:03Z were cancelled, so about 126 of the 169 red minutes came after the fix was in. No merge to master happened while it was red.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells; Select-String -Path ..\logs\DarkFactory-20261007-111121.log -Pattern 'BL-165[01] ci|37694677072|37710969213'
```

- Expected: No spell over 30 minutes.
- Actual: start 2026-10-07T22:19:58Z end 2026-10-08T01:09:09Z minutes 169.18 runId 37694677072

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the reproduction. ciRedSpells holds only the 37733074018 spell. Run 37694677072 is not in ci-runs.json (ciRunsFrom 2026-10-07T23:41:08Z), and DarkFactory-20261007-111121.log does not exist. The spell lies before the saved CI history, so this could not tell.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
