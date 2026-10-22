---
id: BL-3028
title: Make the CI watch file job-step failures with real touches, never let an unready head task stall the lanes, and close fixed CI tasks
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/CiWatch.ps1, DarkFactory/Claim.ps1, RunDarkFactory.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-22
completed:
---
# BL-3028 — Make the CI watch file job-step failures with real touches, never let an unready head task stall the lanes, and close fixed CI tasks

## Goal

A CI failure in a job step (not a named test) is filed with narrow, non-empty `touches`; an unready task at the head of the queue never stops lanes claiming the ready tasks behind it; and the CI watch closes or requeues a CI-fix task once a later green run proves it already fixed.

## Context

Defect observed 2026-10-21/22 (Arizona). At 21:19 the coordinator's CI watch (BL-987, `DarkFactory/CiWatch.ps1`) filed BL-3026 "Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows" with `touches: []`. `Get-CiTouches` derives touches only from a test project (`$verdict.Project`), and a Kind `job` failure (see `Get-CiFailures`) has an empty `Project`, so `New-CiFailureTasks` passed no `-Touches`.

`Get-TaskUnreadyWhy` / `Get-TaskTextUnreadyWhy` in `DarkFactory/Claim.ps1` (BL-3001) mark a task with empty touches unready ("it would run alone and idle every other lane"). BL-3026 was High and #1 in the queue; lane 1 sat in "wait: only unready tasks are ready (BL-3026)" (the `Invoke-Claim` return near the `$unready.Count` check) from 21:39 to 01:30 while BL-3011 and BL-3023 were ready (#2, #3), and every other lane retired. About 4 hours were lost. Start by finding out why `task-board.ps1 next -Skip BL-3026` returned nothing: a likely cause is the board's held-back rule (an empty-touches task overlaps everything, so it reserves everything and holds back the tasks behind it), but confirm that rather than assume it. The board script is a guard file and makes the task interactive only, so fix it in `DarkFactory/` alone (for example, by passing the unready ID to `next` in a way that also releases its reservation, or by choosing the next ready task from `status` output). If it truly cannot be done there, stop and report; do not edit `task-board.ps1`.

The failure had already been fixed by other lanes (commits splitting Unit2413ConnectionPeer.Mismatch and ConnectionPeer.Create), and CI run 39723672567 on 1afd80a99 was green. The CI watch never noticed its task was moot. `Test-CiFailureCovered` only suppresses filing; nothing retires a filed task.

Existing tests: `RunDarkFactory.ps1 -TestCiWatch` (the check blocks at the end of `DarkFactory/CiWatch.ps1`, via the `$check` helper) and `-TestTaskReadiness` / `-TestClaimDuplicates` (blocks in `DarkFactory/Claim.ps1`, using a throwaway origin and lane repo). Extend those; they are plain PowerShell, no Pester. Whether a `-Test...` switch needs adding to the parameter list in `RunDarkFactory.ps1` is for the implementer to check; avoid it if the existing switches suffice.

## Acceptance criteria

- [ ] For a `job` failure, the CI watch derives `touches` from the files and members named in the log (the `::error file=...` lines and "X is outside a gate" members of `Measure-CodeQuality.ps1`, mapped to the file or its project folder as far as they exist in the repo), and falls back to a narrow non-empty list (the workflow or script the step runs, e.g. `Measure-CodeQuality.ps1`), never `[]`. A `-TestCiWatch` case feeds a log shaped like BL-3026's and checks the filed task's touches are non-empty and name the member's file.
- [ ] `Get-CiTaskNewArgs` or `New-CiFailureTasks` never files a non-audit CI task with empty touches; a `-TestCiWatch` case pins this for an unknown step with no file in the log.
- [ ] With an unready task first in the queue (empty touches, High) and two ready tasks behind it, `Invoke-Claim` claims the first ready one behind it instead of returning `Wait`; a `-TestTaskReadiness` case on a throwaway repo proves it and checks the unready task is still traced once.
- [ ] `Invoke-Claim` returns `Wait` with "only unready tasks are ready" only when no ready, claimable task is left; the root cause of the 2026-10-21 wait (why `next` did not offer BL-3011 or BL-3023) is named under Notes.
- [ ] When a later CI run on the branch is green for the job or test a live CI-fix task names (title `Fix CI failure ...`, `Fix CI build ...` or `Fix flaky CI test ...`, in Backlog), and the run's commit contains the task's creation, the watch moves it out of Backlog (to Done is not allowed from Backlog: use Deferred with the reason "already fixed, CI green on <sha>" through `task-board.ps1 move`, in the ci-watch worktree, pushed like the filing). A task in Doing is left alone. A `-TestCiWatch` case covers: green run after the failure closes it; a still-red run leaves it; a Doing task is left; a flaky task is handled the same way.
- [ ] The decision is recorded: either in a short ADR "Decided by Claude under Stewart's delegation" saying that a fixed CI task is Deferred rather than Done, or in Notes if an ADR already covers it.
- [ ] `RunDarkFactory.ps1 -TestCiWatch`, `-TestTaskReadiness` and `-TestClaimDuplicates` all pass; `task-board.ps1` is unchanged (`git diff --stat` shows no change to it); the help text at the top of `RunDarkFactory.ps1` describing the CI watch is updated to say what it now does.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: ee3e0e47 bfef5946 978ecf9e 1f1d7069 b2a44ef2 e35c9f7d 2b993f16.
