---
id: BL-5176
title: Re-fix AF-0707: CI red 74 minutes from run 43110153266 (27be8cbc, Quality gates Measure coverage)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-14
completed:
---
# BL-5176 — Re-fix AF-0707: CI red 74 minutes from run 43110153266 (27be8cbc, Quality gates Measure coverage)

## Goal

The cause of the factory behaviour the audit office reported as AF-0707 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0707 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0707-ci-red-74-minutes-from-run-43110153266-27be8cbc-qu.md`.

Re-fix: the earlier task(s) BL-5004 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-11-13 | 2026-11-13_2321.md | reproduces: yes | Ran the reproduction with -Since 2026-11-05: run 43110153266 is still in ciRedSpells at 74 minutes. The window also holds new spells over 30 minutes without a filing trace.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-11T22:34:57Z to 23:48:57Z, 74 minutes, run 43110153266 on 27be8cbc. The 101854 and 190859 logs repeat the same Quality gates job failure from 15:36 to 16:22 local.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-05 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No spell over 30 minutes
- Actual: A 74-minute spell at run 43110153266

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Deferred. Nothing left to fix in the factory: the finding's Expected line is the 'no spell over 30 minutes' rule Stewart replaced on 2026-11-12. Under the current rule every failed CI run from 2026-11-13 06:48 to 2026-11-14 12:00 Arizona was filed or covered within 10 minutes (11 runs), though two spells ran 67 and 110 minutes while fixes went through CI. PR #343 has the process re-audit apply the current rule, so the next audit can close the finding; look again only if that re-audit still says it reproduces.
- 2026-11-18: Deferred -> Backlog. Stewart 2026-11-18: clear Deferred; returned to Backlog
- 2026-11-19: Backlog -> Deferred. Its finding closed on a reliable re-audit (audit 20261119-013420, closed-how reliable-reaudit); nothing left to fix
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-25: Fix landed: efda64f912 (2026-11-25 ~13:45 Arizona) - CI fixes that changed nothing no longer cancel a running CI run, and a CI fix skips the pre-push quality measure. Waits for a shift on it, then the measurement over logs from that time.
- 2026-11-26: Measured after the fix (Measure-FactoryProcess.ps1 -Since 2026-11-25 15:16 Arizona, shifts 20261125-151637 and -233104, fresh gh run list): 1 CI-red spell, 37.8 min (run 45736721217 on 9c82f174, 18:54-19:32 Arizona) - still over 30. Cause: a Windows-only failure (WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout) was filed at 19:00, but the next CI run only started at 19:17 because the pushes between were task-only ([skip ci]); that run passed before the BL-5994 fix landed at 19:30. Nothing was cancelled and no revert was needed, so efda64f912 and bcebcab086 were not exercised. Next step: rerun the failed jobs at once when a red run fails on one platform only.
