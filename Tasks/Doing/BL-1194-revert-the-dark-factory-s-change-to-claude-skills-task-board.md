---
id: BL-1194
title: Revert the dark factory's change to .claude/skills/task-board/task-board.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1]
lane: no
requirement: none
created: 2026-10-02
completed:
---
# BL-1194 — Revert the dark factory's change to .claude/skills/task-board/task-board.ps1

## Goal

The `audit-guard` job passes on work/dark-factory again: `.claude/skills/task-board/task-board.ps1` there matches master, or its change reaches master through the audit branch.

## Context

Filed by the dark factory's CI watch (BL-999) for audit guard .claude/skills/task-board/task-board.ps1: the `audit-guard` job failed in CI run 37037416525 (https://github.com/StewartScottRogers/Curl/actions/runs/37037416525). First failing commit: 64be4673.

    Audit guard: .claude/skills/task-board/task-board.ps1 changed on work/dark-factory since its merge base with master

Audit paths and their guards change only through the audit branch (ADR-0267). Interactive only: a dark factory lane cannot touch `.claude/skills/task-board/task-board.ps1` (BL-997). Red CI blocks the shift-end merge until this is fixed. Decide whether the change was wanted: if not, restore master's copy on work/dark-factory (`git checkout origin/master -- .claude/skills/task-board/task-board.ps1`); if it was, land it on master through the audit branch's pull request, then merge master into work/dark-factory.

## Acceptance criteria

- [ ] The `audit-guard` job passes on work/dark-factory for the commit that lands the fix.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
