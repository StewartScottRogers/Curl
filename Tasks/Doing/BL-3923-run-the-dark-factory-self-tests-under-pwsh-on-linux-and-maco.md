---
id: BL-3923
title: Run the dark factory self-tests under pwsh on Linux and macOS in CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3920, BL-3921, BL-3922]
touches: [.github/workflows/dark-factory-unix.yml, CLAUDE.md]
requirement: none
created: 2026-10-27
completed:
---
# BL-3923 — Run the dark factory self-tests under pwsh on Linux and macOS in CI

## Goal

A GitHub Actions workflow runs every `RunDarkFactory.ps1 -Test*` self-test under `pwsh` on
`ubuntu-latest` and `macos-latest`, so a change that breaks the dark factory off Windows shows
up red, and `CLAUDE.md` says the factory runs on Linux and macOS through `RunDarkFactory.sh`.

## Context

Last of five tasks making the dark factory run under `pwsh` off Windows (after BL-3920,
BL-3921 and BL-3922). The self-tests are listed in `RunDarkFactory.ps1`'s help examples
(`-TestAlarm`, `-TestOutOfTokens`, `-TestAutoLanes`, `-TestMachineProbe`, `-TestHeartbeat`,
`-TestCiWatch`, `-TestAffectedTests`, `-TestTaskIds`, `-TestAdrIds`, `-TestTaskBudget`,
`-TestModelChoice`, `-TestClaimDuplicates`, `-TestTaskReadiness`, `-TestIntegrateLock`,
`-TestLaneFlags`, `-TestPlatform` and others; take the list from the help, not this note).
Put the job in a new workflow file: `.github/workflows/ci.yml` is a guard file that changes only
through the `audit` branch. The factory also calls `.claude/skills/task-board/task-board.ps1`,
another guard file; if it fails under `pwsh` on Linux or macOS, do not edit it here: file an
interactive (`-NoLane`) task for it and list it in this task's Log.

## Acceptance criteria

- [ ] `.github/workflows/dark-factory-unix.yml` runs on pushes to `work/dark-factory` and `master` and runs each `-Test*` self-test in `RunDarkFactory.ps1`'s help examples under `pwsh` on `ubuntu-latest` and `macos-latest`, one step per self-test, with `-QuietAlarm` where the test sounds.
- [ ] The workflow passes on both runners for the commit that adds it (a link to the green run is in this task's Log), or each failing self-test has its own task filed and named in the Log.
- [ ] `CLAUDE.md`'s "Dark factory" section says shifts run on Linux and macOS with `RunDarkFactory.sh` (PowerShell 7 required), and `DarkFactory/LaneRules.md` agrees if it says otherwise.
- [ ] `dotnet build` and the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 04e7d705 b4230d74 b4c98247 d265299d.
