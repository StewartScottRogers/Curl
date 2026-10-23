---
id: BL-3083
title: Fix AF-0330: CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
lane: no
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3083 — Fix AF-0330: CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push

## Goal

The defect the audit office reported as AF-0330 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0330 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0330-ci-red-48-4-min-on-work-dark-factory-from-run-3969.md`.

Location: `logs/DarkFactory-20261021-150011.log`

Location: `logs/DarkFactory-20261021-150011.log`

ciRedSpells: start 2026-10-21T23:21:56Z, end 2026-10-22T00:10:17Z, 48.35 min, runId 39691487971. First failing commit 01deb2980 ('chore(tasks): complete BL-2856 and file BL-2996...'), job 'Quality gates (windows-latest) step Measure coverage, complexity and CRAP'. The coordinator saw it at 16:23:12 MST and filed BL-2998 at 16:29:31. L1 claimed it at 16:47:29 and pushed DONE at 16:56:44 ('WriteBytesAsync split'). Run 39696631298 then succeeded. About 18 min passed between filing and claim. The lane integrate step logs 'quality ok' but did not catch a member outside the gate that the CI quality job measures.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39691487971
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-21T23:21:56Z, end 2026-10-22T00:10:17Z, minutes 48.35

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The process measurement (`Audit/Tools/Measure-FactoryProcess.ps1 -LogRoot Z:\repos\Curl.logs` with a `-CiRunsJson` built by `gh run list --workflow CI --branch work/dark-factory` (the audit run's own command), counting only lane-trace lines and CI runs at or after the window start), run over the live logs since 2026-10-22 09:54:28 (65f5286ec, the lane prompts' rule that the quality script runs for every production change), shows no red spell over 30 minutes from a quality-gate failure.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22: BL-2998 (Done 2026-10-21) fixed the red quality gate (WriteBytesAsync split) and run 39696631298 went green. The reproduction reads a past audit log copy through audit-office tools a lane may not run; that spell is history and cannot change, and no code change is left in DarkFactory/CiWatch.ps1. Only a later process re-audit can close AF-0330.

- 2026-10-22 (interactive): Cause found: the lanes skipped Measure-CodeQuality.ps1 (BL-2856's run: "not run: the library change adds ... no branch"; BL-3022's run left coverage to BL-3023), so a member outside the gate reached CI. BL-2998 fixed only the member. Fixed by 65f5286ec (09:54:28): both lane prompts now say the script runs for every production change, however small. Baseline before it, 2026-10-22 02:44:38 to 09:50: no red spell, ciRedMinutes 0. Left in Backlog: no window after the fix yet; run the measurement again once a shift has run on 65f5286ec.

- 2026-10-22 (interactive) measured: window 2026-10-22 09:54:28 to 15:00 (65f5286ec on), shifts 20261022-102445, -140915, -143901, logs Z:\repos\Curl.logs, CI runs from gh run list --workflow CI --branch work/dark-factory (500). Measure-FactoryProcess: tasksDone 36, requeues 2 (BL-3121 14:42:56 and BL-3122 14:54:44, both 'Needs Curl.Networking.UnitTests...', none waiting for CI), tasksClaimedMoreThanOnce 1 (BL-3122, one requeue then Done), ciRedMinutes 0, ciRedSpells none. Fast tests: build 0 errors, all passing.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Blocked. Stewart: reproduction reads a past audit log so it cannot change; fix already shipped via BL-2998 - close AF-0330 at the next process re-audit and drop this task?
- 2026-10-22: Blocked -> Backlog. Criterion 1 reworded to the live-window measurement (process findings about past behaviour are measured on logs written after the fix)
- 2026-10-22: Backlog -> Doing. Criteria at claim: 9484213a 9742157d.
- 2026-10-22: Doing -> Done. Live window since 65f5286ec measured: no task requeued twice, no REQUEUE waiting for CI, CI red 0 min; build and fast tests green
