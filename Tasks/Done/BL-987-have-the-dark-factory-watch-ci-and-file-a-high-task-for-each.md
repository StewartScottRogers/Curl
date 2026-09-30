---
id: BL-987
title: Have the dark factory watch CI and file a High task for each new Linux or macOS failure within minutes
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, CLAUDE.md, .claude/hooks/whisper-milestone.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-987 — Have the dark factory watch CI and file a High task for each new Linux or macOS failure within minutes

## Goal

When the `CI` workflow fails on `work/dark-factory`, the running shift files a High-priority task naming the failing test, platform and run within one heartbeat, so a lane fixes it in minutes rather than the break sitting unnoticed until someone asks for status.

## Context

Lanes test only on Windows. On 2026-09-29 two Linux/macOS-only breaks went unnoticed while lanes kept pushing on top of them: the SSH RSA test (fixed by BL-905) and `FromComponents_ZeroCoefficient_Throws`, red from 20:40 to 22:06 UTC across nine runs (fixed by BL-972). A red CI blocks `Invoke-MergeToMaster` at the end of the shift.

Why the coordinator and not each lane: `.github/workflows/ci.yml` has `concurrency: ci-${{ github.ref }}` with `cancel-in-progress: true`, so with several lanes pushing most runs are cancelled and a lane waiting on "its" run usually sees `cancelled`. Only the coordinator sees the whole stream; `Invoke-MergeToMaster` (around line 1647) already reads runs with `gh run list --workflow CI`.

## Acceptance criteria

- [x] On each heartbeat the coordinator reads the latest `CI` runs on the shift's branch that finished `success` or `failure` (ignoring `cancelled`), newest first.
- [x] For a `failure`, it reads the failing test names and jobs from `gh run view <id> --log-failed` (lines `Failed <TestName>`, plus build `error` lines when no test failed).
- [x] It files one High task per failing test (or per build error) with `task-board.ps1 new`, with the test name, the failing platforms, the run id, the first failing commit if found, and the error message in its Context, and `touches` set to that test's project and its library.
- [x] It files nothing for a test that already has a live task naming it (search Backlog, Doing and Blocked for the test name), so repeated red runs file one task, not one per run.
- [x] A test that fails in only one of the last several runs is filed as a flaky-test task with that word in the title, not as a regression.
- [x] It writes a coordinator log line for each task filed, and the whisper hook announces it.
- [x] Documented in the script's header and in `CLAUDE.md`'s dark factory section.

## Notes

- `touches` widened to `CLAUDE.md` (the criterion asks for it documented there) and
  `.claude/hooks/whisper-milestone.ps1` (so "the whisper hook announces it"); no task in
  Doing names either.
- Built in `RunDarkFactory.ps1`'s new "CI watch" section: `Get-CiFailures` (log reading),
  `Get-CiVerdicts` (regression or flaky), `Test-CiFailureCovered` (dedupe), `Invoke-CiWatch`
  (called from the coordinator loop beside `Publish-BoardStatusIfDue`). `-TestCiWatch`
  proves it with 14 cases, the log lines taken from real red run 36637529664; parsing that
  whole 4184-line log yields exactly `FromComponents_ZeroCoefficient_Throws` on Linux and
  macOS in `Curl.Protocol.Ssh.UnitTests`. A read-only dry run against `work/dark-factory`
  (all green now) found six finished runs and nothing to file.
- Choices taken (sensible defaults):
  - Regression vs flaky needs history, but the goal wants minutes. A failure in the newest
    run is filed at once as a regression when it broke the build, failed on two platforms,
    or failed in the run before too; one failing on a single platform in only the newest
    run waits for the next finished run (flaky if that passes it, regression if it fails
    it), or 30 minutes, then files as a regression. Window: last six passed-or-failed runs.
  - Dedupe: a live task (Backlog/Doing/Blocked) naming the test, matched as a whole word,
    covers it. A finished task naming it also covers a failing run whose commit does not
    yet contain that task's last commit (the failure predates the fix), so a stale red run
    finishing after the fix lands does not refile, while a real recurrence does.
  - Tasks are filed in a detached worktree `<repo>.lanes\ci-watch` at `origin/<branch>` and
    pushed straight to the branch (three tries), so the coordinator's own checkout is not
    touched until shift end, like the auto-board worktree; removed at shift end. Lanes'
    later `dedupe` renumbers any clashing IDs of theirs.
  - Filed tasks use pipeline `feature` (as BL-972 did) and titles "Fix CI failure <test> on
    <platforms>", "Fix flaky CI test <test> that failed once on <platforms>", "Fix CI build
    error CSxxxx in File.cs on <platforms>"; the hook recognises the `fix-ci-` /
    `fix-flaky-ci-` slugs. The coordinator is not a Claude session, so it feeds the hook a
    synthetic PostToolUse JSON through a hidden process.
  - `--jq` is not used: Windows PowerShell strips the double quotes jq needs from native
    arguments; `--json` plus `ConvertFrom-Json` instead.
  - Only a lane shift's coordinator watches; a single-runner shift does not (it has no
    coordinator loop and CI there is not racing several lanes).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Lane shift coordinator watches CI each heartbeat and files a High task per new Linux/macOS test or build failure (flaky ones labelled), deduped against live tasks; -TestCiWatch 14/14
