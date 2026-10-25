---
id: BL-3453
title: Fix AF-0406: BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-24
completed:
---
# BL-3453 — Fix AF-0406: BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap

## Goal

The cause of the factory behaviour the audit office reported as AF-0406 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0406 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0406-bl-3234-requeued-twice-waiting-on-unfinished-prere.md`.

Location: `logs/DarkFactory-20261023-110315-L1.log:11`

Location: `logs/DarkFactory-20261023-110315-L1.log:11`

Claim 1, L3 04:41:42 (opus feature, 28 turns): REQUEUE 04:46:47 'Waiting on BL-3284 to BL-3288 (the protocol and proxy server ports, split out for size) and BL-3235'. Claim 2, L1 11:04:17 (opus retry, 24 turns): REQUEUE 11:08:44 'Waiting on BL-3334 (the SOCKS4/SOCKS5 half of proxy_server.py)'. At 17:14:44 L3 logged it 'unclaimable cost cap 1.97 of 3.07' and split it into BL-3424..BL-3427 at 17:16:23. Both Opus runs did nothing but find that a prerequisite task was not done. The board offered it as ready without the dependency declared.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3234 (claim|REQUEUE|split|unclaimable)'
```

- Expected: BL-3234 not claimable until BL-3284..3288, BL-3235 and BL-3334 were done
- Actual: two claims each ending in a REQUEUE naming an unfinished prerequisite, then unclaimable and split

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause for BL-3234: the board did honour its depends-on. Claim 1 added BL-3284..BL-3288 and BL-3235 and requeued. Claim 2 came only after those were Done, found the SOCKS half missing, filed BL-3334 and requeued. Claim 1's one-pass prerequisite check missed it. Both runs' cost came off the cap, so the next claim was unclaimable at $1.97 of $3.07 and the task was split into BL-3424..BL-3427. Under the fix both runs are waived, so the claim that does the work runs with a whole cap. The missed prerequisite itself is still left to the prompt's one-pass rule.
- Fix: 9c6c5732f "fix(factory): a run that only finds prerequisites does not spend the cost cap (ADR-0812)", landed on origin/work/dark-factory 2026-10-24 08:49 Arizona (with 8bf4e3854, which corrects a stale -TestModelChoice case). It covers AF-0387, AF-0404, AF-0405 and AF-0406 (BL-3364, BL-3451, BL-3452, BL-3453), which share one cause: a first claim found only prerequisite work and requeued with no code, and AF-0095's daily rule took its cost off the cap of the claim that did the work, so that claim stopped at the cap with its work stashed, or was unclaimable, and was split.
- Now `Register-PrerequisiteRequeueRun` (DarkFactory/TaskBudget.ps1, called in DarkFactory/ShiftRunner.ps1 before Save-StrayChanges) writes `<ID>-<stamp>[-L<n>].prerequisite-requeue.txt` beside the run log and traces `waive` when the task is in Backlog, nothing is left to stash, no stash of the task waits, and its depends-on names an ID not Done. `Get-TaskRunSpends` skips such runs. The lane prompt (DarkFactory/Prompts.ps1) says so, puts a Doing task holding a needed file into depends-on, and has tasks filed together name the ones they extend in depends-on.
- Verified 2026-10-24: dotnet build clean; fast tests green (46 test projects); RunDarkFactory.ps1 -TestTaskBudget (7 new cases), -TestTaskStash, -TestTaskIds, -TestModelChoice, -TestClaimDuplicates and -TestTaskReadiness pass.
- To measure once a shift has run: the finding's measurement over Z:\repos\Curl.logs from 2026-10-24 08:49. BL-3234 is now Deferred, so check the pattern for any task: every `waive` trace line, no task `unclaimable` or split over its cost cap after prerequisite REQUEUEs, and how many tasks still have two prerequisite REQUEUEs. No shift had run since the fix when this was written, so the task waits in Backlog.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
- 2026-10-24: Doing -> Backlog. Fix 9c6c5732f (ADR-0811) landed 2026-10-24 08:49; waits for a shift to run since then so the live-log measurement can be taken (ADR-0761)
- 2026-10-24: ADR-0811 named above for this fix was renumbered ADR-0812 (two ADRs took 0811 at once).
- 2026-10-25: Backlog -> Doing. Interactive session: measure the live logs after fix 9c6c5732f and 8bf4e3854 (ADR-0761) Criteria at claim: 87cd20bd 9742157d.
