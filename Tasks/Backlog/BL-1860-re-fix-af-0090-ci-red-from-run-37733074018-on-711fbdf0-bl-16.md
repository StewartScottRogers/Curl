---
id: BL-1860
title: Re-fix AF-0090: CI red from run 37733074018 on 711fbdf0 (BL-1609's Done commit) for 321 minutes (the tool says 179); its three fix tasks waited 5 hours for the next shift
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-09
completed:
---
# BL-1860 — Re-fix AF-0090: CI red from run 37733074018 on 711fbdf0 (BL-1609's Done commit) for 321 minutes (the tool says 179); its three fix tasks waited 5 hours for the next shift

## Goal

The defect the audit office reported as AF-0090 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0090 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0090-ci-red-from-run-37733074018-on-711fbdf0-bl-1609-s.md`.

Re-fix: the earlier task(s) BL-1768 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the reproduction: ciRedSpells still lists start 2026-10-08T05:41:00Z, end 11:01:51Z, 320.85 minutes, runId 37733074018. It is the same incident, inside the window because -Since is a date. The DarkFactory-20261007-201432 and 20261008-034419 logs are not in this copy. No new spell in the window crossed 30 minutes.

Location: `logs/DarkFactory-20261007-201432.log:70`

Location: `logs/DarkFactory-20261007-201432.log:70`

ciRedSpells[1]: start 2026-10-08T05:41:00Z, run 37733074018, end 08:40:00Z, 179 minutes. That end is the fake run 9453587099 (see the ci-runs-unknown-commits finding). The real end is run 37766645992, success on 3e8da37c at 11:01:51Z: 320.85 minutes. Shift 20261007-201432 ended at 22:34:54 local (nothing ready). At 22:41:18 it logged 'merge not merged: CI failure on 711fbdf', and at 22:41:46 it filed BL-1716 (RunAsync_NumericOptionWithValueOutsideItsValidPartition_~), BL-1717 (RunAsync_WriteDelaysWithACurlTimer_TakeRealTime on Windows) and BL-1718 (FindFirstDifference_Protocol_Strips~). It then closed with 'shift complete with nothing waiting on Stewart'. No lane ran until shift 20261008-034419 started at 03:44:20; BL-1716 was claimed at 03:44:32 and BL-1718 was done at 3e8da37 by 03:54. PR #71 merged 3e8da37 only after run 37766645992 passed on it, so there was no merge while red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells; Select-String -Path ..\logs\DarkFactory-20261007-201432.log,..\logs\DarkFactory-20261008-034419.log -Pattern 'shift +(start|end)|not merged|BL-171[678] ci|merged 3e8da37'
```

- Expected: No spell over 30 minutes; CI-fix tasks claimed soon after filing.
- Actual: start 2026-10-08T05:41:00Z end 2026-10-08T08:40:00Z minutes 179 runId 37733074018 (real end 11:01:51Z); fix tasks filed 22:41:46, first claim 03:44:32

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
