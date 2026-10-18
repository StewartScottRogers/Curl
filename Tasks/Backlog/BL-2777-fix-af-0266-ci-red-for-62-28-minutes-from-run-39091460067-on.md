---
id: BL-2777
title: Fix AF-0266: CI red for 62.28 minutes from run 39091460067 on 1c266d83 (BL-2551): ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne failed on Windows; fix done in 18 min, then 5 cancelled runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed:
---
# BL-2777 — Fix AF-0266: CI red for 62.28 minutes from run 39091460067 on 1c266d83 (BL-2551): ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne failed on Windows; fix done in 18 min, then 5 cancelled runs

## Goal

The defect the audit office reported as AF-0266 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0266 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0266-ci-red-for-62-28-minutes-from-run-39091460067-on-1.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-18T03:04:00Z to 04:06:17Z, 62.28 min, run 39091460067. Commit 1c266d83 is BL-2551's 'port libtest programs 2505 to 3100'. The failing test was ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne on Windows: a port-collision race, where a bare NullReferenceException hit when the port was taken. DarkFactory-20261017-165834.log:32 saw the failure at 20:05:03 local; line 33 filed BL-2707 at 20:11:45. BL-2707 was claimed on L2 at 20:17:48 and DONE at 20:22:44 (03:22Z), with a test-only fix. CI stayed red for 44 more minutes: runs 39092490535, 39092732722, 39093406732, 39094005620 and 39094393407 were all cancelled by newer pushes before finishing, and green came with 39094990782 (created 03:49:55Z, success 04:06:17Z). Pushes every few minutes keep cancelling 10 to 15 minute CI runs, so a fixed branch keeps showing red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-*.log -Pattern '39091460067|BL-2707 (claim|DONE)'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-18T03:04:00Z 2026-10-18T04:06:17Z 62.28 39091460067 False; BL-2707 filed 20:11:45, DONE 20:22:44.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
