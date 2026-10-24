---
id: BL-3363
title: Fix AF-0386: BL-3263 claimed 3 times because two tasks shared its ID, so lanes 6 and 2 both made the same differential-normaliser change
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-23
completed:
---
# BL-3363 — Fix AF-0386: BL-3263 claimed 3 times because two tasks shared its ID, so lanes 6 and 2 both made the same differential-normaliser change

## Goal

The cause of the factory behaviour the audit office reported as AF-0386 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0386 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0386-bl-3263-claimed-3-times-because-two-tasks-shared-i.md`.

Location: `logs/DarkFactory-20261023-005934-L2.log:64`

Location: `logs/DarkFactory-20261023-005934-L2.log:64`

The tool reports BL-3263 claims=3. The shift-end review created BL-3263 (ConcatenatedReadStream), and a second task (differential normaliser progress meter) was given the same ID. L6 claimed BL-3263 at 01:34:55 and did the normaliser change (BL-3263-20261023-005934-L6.jsonl result: 'the differential normaliser now reduces the verbose progress meter...'). It hit a rebase conflict at 01:39:03, renumbered its task 'BL-3263 -> BL-3267' at 01:42:45, and pushed at 01:43:42. L2 claimed BL-3263 at 01:38:51, before the renumbering, and made the same normaliser change again (L2 jsonl result: 'DifferentialOutputNormaliser now collapses the verbose progress meter...'). It then spent two resolve runs (01:44:05 and 01:52:44) on rebase conflicts against L6's identical work, and pushed at 02:00:45. That is 22 lane-minutes redone. L7 then claimed the real task at 01:51:58 and finished it at 02:14:05. BL-3267's Log says 'moved by hand: the board script refused over duplicate ID'. Cause: the board gave out a duplicate ID and both lanes claimed it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-005934-L*.log -Pattern 'BL-3263 (claim|renum|resolve|DONE)'
```

- Expected: BL-3263 claimed once; every task ID unique on the board.
- Actual: tasks[BL-3263].claims = 3; claims by L6, L2 and L7, and 'renum BL-3263 -> BL-3267' in L6 at 01:42:45.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause (2026-10-24, from the 20261023-005934 logs and git history): two faults. (1) The shift-end review (DarkFactory/ShiftEnd.ps1) numbers its tasks in a worktree at the reviewed commit; lane 4 had filed BL-3263 (normaliser) at 01:21:07 (09cccdcef) and lane 6 claimed it at 01:32:49, then the review pushed its own BL-3263 (ConcatenatedReadStream, 7b0a471fc) at 01:33:23 without deduping against the fresh branch. (2) Lane 2's claim at 01:38:51 was refused by the board ("names 2 tasks"), but Invoke-Claim (DarkFactory/Claim.ps1) checked only `Get-TaskState BL-3263 -eq 'Doing'`, which found lane 6's twin, so its empty claim ("git commit: nothing to commit") passed for a claim and it redid lane 6's work. task-board.ps1 already refuses a shared ID, so no guard-file change was needed.

Fix: 3775626b6, landed on work/dark-factory 2026-10-24 08:46 Arizona. Push-ReviewCommitsUnderLock now runs `Invoke-ReviewDedupe` (`dedupe -Since origin/<branch>` after the rebase, committed with [skip ci]); Invoke-Claim treats a "names N tasks" refusal as a refusal whatever Get-TaskState says, and renumbers before going on. Two new -TestClaimDuplicates cases, both failing without the fix; -TestClaimDuplicates, -TestIntegrateLock, -TestTaskIds, -TestTaskReadiness and -TestHeartbeat pass; `dotnet build` and the fast tests pass.

To measure (criterion 1, open): after a shift has run on 3775626b6 or later, run `Measure-FactoryProcess.ps1 -Since 2026-10-24 -LogRoot Z:\repos\Curl.logs` with a fresh CI list, counting from 08:46 on 2026-10-24: no task with claims >= 2 whose claims came from two different tasks sharing one ID, no `claim` line right after a "nothing to commit" git FAIL, and any `renum ... review:` lines show the review's IDs renumbered before they reached the branch.

## Log

- 2026-10-23: Created.
- 2026-10-24: Backlog -> Doing. Interactive session fixes AF-0386 duplicate-ID claims Criteria at claim: 87cd20bd 9742157d.
- 2026-10-24: Doing -> Backlog. Fix 3775626b6 landed 2026-10-24 08:46; live-log measurement needs a shift run after it
