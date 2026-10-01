---
id: BL-1022
title: Announce a due audit at shift end and hold a milestone merge to master until it runs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1019, BL-999]
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1022 — Announce a due audit at shift end and hold a milestone merge to master until it runs

## Goal

At the end of every shift the factory says whether an audit is due and why, skips the shift-end merge to `master` when the reason is a completed roadmap milestone, and refuses to start a shift while an audit is running.

## Context

Interactive only (`lane: no`): it changes how the factory yields to its auditors. Run it
with `/task-run BL-1022`. Cadence from the ADR recorded by BL-994: audits run before each
roadmap-milestone merge to `master` and after changes to `RunDarkFactory.ps1`, and
between shifts, never during one.

`RunDarkFactory.ps1` today merges its branch into `master` at the end of a shift through
`Invoke-MergeToMaster` (around line 1675), only when the `CI` workflow passed for the
exact commit.

Design:

- At shift end, before `Invoke-MergeToMaster`, run master's copy of the check - `git show
  origin/master:Audit/Tools/Test-AuditDue.ps1` into a temporary file, as the CI guard
  does, so the factory never runs its own copy of an audit tool - with `-Json`. If
  `origin/master` has no copy yet, skip all of this silently.
- When due: add `Audit due: <reasons>` to the end-of-shift report and speak it with the
  end-of-shift notice (not the alarm).
- When a reason is `milestone:<N>`: do not merge; the merge result reads `not merged:
  audit due before the Milestone <N> merge (run Audit\RunAudit.cmd)`. Other reasons do
  not hold the merge.
- At shift start: if a process whose command line contains `RunAudit.ps1` is running,
  refuse to start with `An audit is running; shifts start between audits.` (with
  `-Continuous`, wait and retry every 5 minutes instead, with a notice).

## Acceptance criteria

- [ ] A new switch `-TestAuditCadence` prints `PASS` and no `FAIL` for: no copy on master skips silently; `Audit due: factory-script` is reported and the merge proceeds; `milestone:3` is reported and the merge is skipped with the message above; `No audit due` adds nothing; a faked running `RunAudit.ps1` process refuses a shift start, and with `-Continuous` waits instead.
- [ ] The script header describes the audit check at shift end and the start refusal.
- [ ] Every existing `-Test*` switch still prints no `FAIL` line.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
