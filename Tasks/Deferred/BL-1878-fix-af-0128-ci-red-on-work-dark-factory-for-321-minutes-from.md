---
id: BL-1878
title: Fix AF-0128: CI red on work/dark-factory for 321 minutes from run 37733074018 on 711fbdf0
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-09
completed:
---
# BL-1878 — Fix AF-0128: CI red on work/dark-factory for 321 minutes from run 37733074018 on 711fbdf0

## Goal

The defect the audit office reported as AF-0128 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0128 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0128-ci-red-on-work-dark-factory-for-321-minutes-from-r.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-08T05:41:00Z, end 2026-10-08T11:01:51Z, minutes 320.85, runId 37733074018, stillRed false. ci-runs.json: 37733074018 failure on 711fbdf0 (05:34:39Z), 37733666115 failure on f06bf08c (05:41:46Z), then no CI run at all for five hours, then eight cancelled runs from 10:44Z to 10:51Z (f4967c9f to bede0122), then 37766645992 success on 3e8da37c (10:55Z). The logs are local time, UTC-7 (6687000's CI ran at 18:07Z and its merge is logged at 11:13). So the spell ran overnight (22:41 to 04:01 local), when no shift pushed. The fix commit came from a shift whose logs are not in the copied folder. The next master merge (6687000, pull request #74, DarkFactory-20261008-082227.log:50) went in only after its own green CI runs, so there was no merge with CI red, which keeps this at Low.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-08 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells
```

- Expected: No red spell over 30 minutes.
- Actual: One spell: 2026-10-08T05:41:00Z to 2026-10-08T11:01:51Z, 320.85 minutes, runId 37733074018.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Blocked. Stewart: AF-0128 is a past 321-minute red spell in historical logs (no shift ran overnight); its reproduction needs the audit office's tool and logs outside a lane's reach, and no RunDarkFactory.ps1 change can alter that history - reject the finding or re-audit it interactively?
- 2026-10-09: Blocked -> Deferred. No change can alter historical logs; PR #89 re-audits process findings by mechanism over the current window, so the next re-audit decides AF-0128
