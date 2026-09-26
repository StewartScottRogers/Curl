---
id: BL-014
title: Harden hrdrClaudeNative.cmd
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
requirement: none
created: 2026-09-25
completed:
---
# BL-014 — Harden `hrdrClaudeNative.cmd`

## Goal

`hrdrClaudeNative.cmd` fails loudly when the Claude Code install fails, and it
handles paths that contain an apostrophe.

## Context

Formerly Icebox item IB-001. These are known minor gaps with no current impact: the
script works on the supported paths. The root `CLAUDE.md` says not to change this
script unless asked, so moving this task to `Backlog` counts as the asking.

## Acceptance criteria

- [ ] The script checks the exit code of `npm install -g @anthropic-ai/claude-code`
      and stops with a clear message when it is non-zero.
- [ ] The PowerShell `--cwd` and `--label` arguments are quoted so a path containing
      `'` works.

## Notes

## Log

- 2026-09-25: Migrated from the Icebox of Documentation/Planning/Backlog.md as IB-001, renumbered BL-014 so the board has one ID sequence.
