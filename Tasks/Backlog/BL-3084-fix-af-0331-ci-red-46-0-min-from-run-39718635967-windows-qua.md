---
id: BL-3084
title: Fix AF-0331: CI red 46.0 min from run 39718635967 (Windows quality gate after BL-3022). Its fix task BL-3026 was filed with empty touches, so no lane would claim it, and lane 1 idled about 230 min on it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
lane: no
requirement: none
created: 2026-10-22
completed:
---
# BL-3084 — Fix AF-0331: CI red 46.0 min from run 39718635967 (Windows quality gate after BL-3022). Its fix task BL-3026 was filed with empty touches, so no lane would claim it, and lane 1 idled about 230 min on it

## Goal

The defect the audit office reported as AF-0331 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0331 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0331-ci-red-46-0-min-from-run-39718635967-windows-quali.md`.

Location: `logs/DarkFactory-20261021-203534-L2.log:84`

Location: `logs/DarkFactory-20261021-203534-L2.log:84`

ciRedSpells: start 2026-10-22T04:12:26Z, end 04:58:27Z, 46.02 min, runId 39718635967. First failing commit d6d35bf61 ('chore(tasks): complete BL-3022'). The gate failure was Unit2413ConnectionPeer.Mismatch outside a gate. The coordinator filed BL-3026 at 21:19:14. At 21:40:26 L2 logged 'BL-3026 unready not claimed: its touches are empty or *, so it would run alone and idle every other lane'. DarkFactory-20261021-203534-L1.log then has 198 'only unready tasks are ready (BL-3026)' wait lines, from 21:40:40 to 01:29:25, and the shift ended '(nothing ready)' at 01:30:31. CI turned green through the BL-3021 splits (success run 39723672567 on 1afd80a99), not through BL-3026. BL-3026 was closed by commit 167e765b2 as 'already fixed'. The CI watch filed a High CI-fix task with no touches, which made it unclaimable by lanes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39718635967; (Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-20261021-203534-L*.log -Pattern 'BL-3026').Count
```

- Expected: No red spell over 30 minutes, and a CI-fix task that a lane can claim.
- Actual: minutes 46.02; 203 BL-3026 lines, 202 of them 'unready' waits or refusals.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Interactive only: the finding's reproduction runs the audit office's process measuring tool over audit logs, which the audit guard refuses a lane. The cause is already fixed by BL-3028 (597082cb8): CiWatch.ps1 files CI tasks with real touches, never empty; `RunDarkFactory.ps1 -TestCiWatch` passes (checked 2026-10-22). Remove lane: no once an interactive session has run the reproduction and ticked the boxes.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Backlog. Interactive only: reproduction needs audit paths; cause already fixed by BL-3028
