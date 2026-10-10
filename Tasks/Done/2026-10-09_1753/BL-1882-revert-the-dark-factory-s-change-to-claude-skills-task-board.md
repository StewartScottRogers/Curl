---
id: BL-1882
title: Revert the dark factory's change to .claude/skills/task-board/task-board.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1]
lane: no
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1882 — Revert the dark factory's change to .claude/skills/task-board/task-board.ps1

## Goal

The `audit-guard` job passes on work/dark-factory again: `.claude/skills/task-board/task-board.ps1` there matches master, or its change reaches master through the audit branch.

## Context

Filed by the dark factory's CI watch (BL-999) for audit guard .claude/skills/task-board/task-board.ps1: the `audit-guard` job failed in CI run 37931431648 (https://github.com/StewartScottRogers/Curl/actions/runs/37931431648). First failing commit: ea846a2e.

    Audit guard: .claude/skills/task-board/task-board.ps1 changed on work/dark-factory since its merge base with master

Audit paths and their guards change only through the audit branch (ADR-0267). Interactive only: a dark factory lane cannot touch `.claude/skills/task-board/task-board.ps1` (BL-997). Red CI blocks the shift-end merge until this is fixed. Decide whether the change was wanted: if not, restore master's copy on work/dark-factory (`git checkout origin/master -- .claude/skills/task-board/task-board.ps1`); if it was, land it on master through the audit branch's pull request, then merge master into work/dark-factory.

## Acceptance criteria

- [x] work/dark-factory's task-board.ps1 equals master's again (45d1dc049); the guard fixes and the help text go through the audit branch (PR #91).

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing. Interactive
- 2026-10-09: Doing -> Done. Reverted in 45d1dc049
