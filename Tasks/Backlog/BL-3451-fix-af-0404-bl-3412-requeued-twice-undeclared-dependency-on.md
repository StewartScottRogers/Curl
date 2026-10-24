---
id: BL-3451
title: Fix AF-0404: BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split
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
# BL-3451 — Fix AF-0404: BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split

## Goal

The cause of the factory behaviour the audit office reported as AF-0404 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0404 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0404-bl-3412-requeued-twice-undeclared-dependency-on-bl.md`.

Location: `logs/DarkFactory-20261023-171352-L3.log:25`

Location: `logs/DarkFactory-20261023-171352-L3.log:25`

Claim 1, L4 14:44:45 (opus, 25 turns, $0.73): REQUEUE 14:55:22 'Depends on BL-3410: both cases run runtests.pl ... criteria were template placeholders' (DarkFactory-20261023-110315-L4.log:237). Claim 2, L3 17:16:23 (opus retry, 47 turns, $2.19): 'stash uncommitted work kept' at 17:26:06, then REQUEUE 'Code done and Conformance tests green (stash); cost cap reached before the quality measurement and solution fast tests'. At 19:13 lanes L1, L2 and L3 all logged it 'unclaimable cost cap: 2.92 of its 3.00'. L1 split it into BL-3438 (land the stashed work, $1.25) and BL-3439. Cause: the task was ready with placeholder criteria and no declared dependency on BL-3410, so its first Opus run was wasted. The second run ran out of cap before the gates. Measured cost $2.92 (6.7x the median).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3412 (claim|REQUEUE|stash|split|unclaimable)'
```

- Expected: BL-3412 claimed once, after BL-3410 was done, and finished within its cap
- Actual: two claims, two REQUEUEs (dependency, then cost cap with a stash), three unclaimable lines and a split into BL-3438 and BL-3439

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause for BL-3412: it was filed ready beside BL-3410 with no depends-on on it, so its first claim ($0.73) found only that dependency. That $0.73 came off the second claim's cap, which stopped at $2.19 of the $2.27 left of $3.00 with its work stashed, before the gates ran. The task was then unclaimable at $2.92 and split. Under the fix the first run is waived (it added BL-3410 to depends-on and left no code), so the second claim gets the whole $3.00. The prompt now has tasks filed together name the ones they extend in depends-on.
- Fix: 9c6c5732f "fix(factory): a run that only finds prerequisites does not spend the cost cap (ADR-0812)", landed on origin/work/dark-factory 2026-10-24 08:49 Arizona (with 8bf4e3854, which corrects a stale -TestModelChoice case). It covers AF-0387, AF-0404, AF-0405 and AF-0406 (BL-3364, BL-3451, BL-3452, BL-3453), which share one cause: a first claim found only prerequisite work and requeued with no code, and AF-0095's daily rule took its cost off the cap of the claim that did the work, so that claim stopped at the cap with its work stashed, or was unclaimable, and was split.
- Now `Register-PrerequisiteRequeueRun` (DarkFactory/TaskBudget.ps1, called in DarkFactory/ShiftRunner.ps1 before Save-StrayChanges) writes `<ID>-<stamp>[-L<n>].prerequisite-requeue.txt` beside the run log and traces `waive` when the task is in Backlog, nothing is left to stash, no stash of the task waits, and its depends-on names an ID not Done. `Get-TaskRunSpends` skips such runs. The lane prompt (DarkFactory/Prompts.ps1) says so, puts a Doing task holding a needed file into depends-on, and has tasks filed together name the ones they extend in depends-on.
- Not covered mechanically: a dependency the filer left undeclared is still found by a run. That run is now waived, and the filing rule is prompt-only.
- Verified 2026-10-24: dotnet build clean; fast tests green (46 test projects); RunDarkFactory.ps1 -TestTaskBudget (7 new cases), -TestTaskStash, -TestTaskIds, -TestModelChoice, -TestClaimDuplicates and -TestTaskReadiness pass.
- To measure once a shift has run: the finding's measurement over Z:\repos\Curl.logs from 2026-10-24 08:49. BL-3412 is now Deferred, so check the pattern for any task: every `waive` trace line, and no task with a dependency REQUEUE followed by a cost-cap REQUEUE with a stash, an `unclaimable` cost-cap line or a cost-cap split. No shift had run since the fix when this was written, so the task waits in Backlog.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
- 2026-10-24: Doing -> Backlog. Fix 9c6c5732f (ADR-0811) landed 2026-10-24 08:49; waits for a shift to run since then so the live-log measurement can be taken (ADR-0761)
- 2026-10-24: ADR-0811 named above for this fix was renumbered ADR-0812 (two ADRs took 0811 at once).
