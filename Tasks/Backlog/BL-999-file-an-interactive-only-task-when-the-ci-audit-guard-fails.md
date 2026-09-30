---
id: BL-999
title: File an interactive-only task when the CI audit guard fails on the shift's branch
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-996, BL-998]
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-999 — File an interactive-only task when the CI audit guard fails on the shift's branch

## Goal

When the `audit-guard` job fails on the shift's branch, the coordinator's CI watch files one High `lane: no` task per offending path, "Revert the dark factory's change to <path>", and says so in the shift's end report, instead of filing nothing.

## Context

Interactive only (`lane: no`): it changes how the factory reacts to its own audit guard.
Run it with `/task-run BL-999`.

BL-987 built the CI watch (`Invoke-CiWatch`, `Get-CiFailures`, `Get-CiVerdicts`,
`Test-CiFailureCovered` in `RunDarkFactory.ps1`). `Get-CiFailures` recognises only
`Failed <TestName>` lines and compiler `error` lines from `gh run view <id>
--log-failed`, and `Invoke-CiWatch` skips a failed run in which it found neither
(`if ($conclusion -eq 'failure' -and -not $failures.Count) { continue }`). So the guard's
failure (BL-998), which prints `Audit guard: <path> changed on work/dark-factory since
its merge base with master`, would today be ignored: red CI still blocks the merge, but
nobody is told why. A lane cannot fix it either - the hook (BL-997) refuses it any
command that names an audit path - so the task must be interactive only.

Design:

- `Get-CiFailures` recognises `Audit guard: (\S+) changed on` as `Kind = 'audit'`,
  `Key = 'audit guard <path>'`.
- Filing an audit item: `task-board.ps1 new -NoLane -Priority High -Pipeline direct
  -Title "Revert the dark factory's change to <path>" -Touches <path>`, body naming the
  run, its link and the first failing commit, with acceptance "the `audit-guard` job
  passes on work/dark-factory". BL-996 lets a factory process file an audit-path task
  only with `-NoLane`, which is what this uses. No flaky classification: one failed run
  files it.
- Dedupe as for tests: a live task naming the path covers it.
- The shift's end report lists these tasks under a line saying they need an interactive
  session, and the alarm at the end of the shift fires as it does for work that needs
  Stewart.

## Acceptance criteria

- [ ] `-TestCiWatch` gains cases that print `PASS` and no `FAIL`: a `--log-failed` excerpt with the line `audit-guard<TAB>Run guard<TAB>2026-09-29T10:00:00.0000000Z Audit guard: Audit/Findings/x.md changed on work/dark-factory since its merge base with master` yields one `audit` item keyed `audit guard Audit/Findings/x.md`; filing it produces a task with `lane: no`, priority High, and `touches: [Audit/Findings/x.md]`; a second run with the same line files nothing.
- [ ] Filing succeeds with `CURL_DARK_FACTORY_LANE=0` set (the coordinator's value), proving the `-NoLane` exemption from BL-996 is used.
- [ ] The end-of-shift report and alarm mention an open audit-guard task.
- [ ] The script header's CI watch paragraph describes the audit-guard case.
- [ ] Every existing `-Test*` switch still prints no `FAIL` line.

## Notes

## Log

- 2026-09-29: Created.
