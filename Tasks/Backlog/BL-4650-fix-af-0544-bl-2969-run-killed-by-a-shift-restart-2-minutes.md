---
id: BL-4650
title: Fix AF-0544: BL-2969 run killed by a shift restart 2 minutes in; the new shift gave it to the wrong lane and it stayed orphaned in Doing
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4650 — Fix AF-0544: BL-2969 run killed by a shift restart 2 minutes in; the new shift gave it to the wrong lane and it stayed orphaned in Doing

## Goal

The cause of the factory behaviour the audit office reported as AF-0544 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0544 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0544-bl-2969-run-killed-by-a-shift-restart-2-minutes-in.md`.

Location: `logs/BL-2969-20261102-083146-L2.jsonl`

Location: `logs/BL-2969-20261102-083146-L2.jsonl`

unfinishedRuns lists BL-2969-20261102-083146-L2.jsonl: 56002 bytes, no result line, empty .err.txt. DarkFactory-20261102-083146-L2.log:137-140 has claim 09:49:11, build ok 09:50:17 and test 8582/8582 at 09:51:09. The coordinator then logged '09:51:12 shift end (restarted: replaced by a new shift)' (DarkFactory-20261102-083146.log), which killed the run. The new coordinator logged '09:51:19 BL-2969 adopt held by no lane; lane 1's worktree has uncommitted work, so that lane resumes it' and '09:51:47 lane 1 adopts BL-2969'. But the run had been in lane 2, and lane 1's uncommitted work was BL-4507's (L1 had claimed BL-4507 at 09:35:09). Lane 1 then logged '09:51:50 sync stash uncommitted work kept: git stash list' and claimed BL-4507 at 09:52:11, redoing it in 4.3 min after the coordinator had requeued it as 'no work for it anywhere'. BL-2969 is never claimed again in the window and is still in Tasks/Doing at the audited commit. The run used only Bash and Skill tools with no file edits, so no work was lost. The cost is a task stuck in Doing for about 8 hours and one task redone.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-*.log -Pattern 'BL-2969|restarted|lane 1 adopts'
```

- Expected: unfinishedRuns empty; an adopted task is resumed by the lane whose worktree holds its work
- Actual: unfinishedRuns contains BL-2969-20261102-083146-L2.jsonl; lane 1 'adopts' BL-2969 at 09:51:47, then stashes and claims BL-4507; BL-2969 never resumes

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix, part 1 (an interactive session, 2026-11-02 17:40 Arizona): 7ecfdd99f - the previous-shift lookup skips the starting shift's own empty `lanes-<stamp>` folder, so a restarted shift adopts each stopped lane's task from that lane's state instead of treating every Doing task as an orphan (the root of AF-0544: both BL-2969 and BL-4507 looked orphaned at 09:51:19).
- Fix, part 2: 3a15c59d1, pushed to work/dark-factory 2026-11-03 10:36:45 Arizona (17:36:45Z). `Get-OrphanAction` (DarkFactory/Park.ps1) now takes `-LaneLastTask`, each dirty lane's task by its newest run log (`Get-LaneLastRunTask`): a dirty lane whose last run was the orphan adopts it, and a dirty lane whose last run was another task is no candidate, so lane 1 can no longer adopt lane 2's BL-2969 over its own BL-4507 work. ShiftCoordinator.ps1 passes the map and traces why a lane adopts. `-TestPark` adds three cases (21/21 PASS under Windows PowerShell 5.1 and pwsh 7).
- Evidence still owed: the measurement window starts at 2026-11-03T17:36:45Z; it needs shift logs written after that, so the task waits in Backlog for an interactive session to run the measurement.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (7ecfdd99f, 3a15c59d1 at 2026-11-03 10:36 Arizona); the post-fix measurement needs shift logs written after it, so it waits in Backlog per its criterion.
