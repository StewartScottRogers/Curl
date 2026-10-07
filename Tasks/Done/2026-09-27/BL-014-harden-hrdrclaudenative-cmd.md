---
id: BL-014
title: Harden hrdrClaudeNative.cmd
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [hrdrClaudeNative.cmd]
requirement: none
created: 2026-09-25
completed: 2026-09-27
---
# BL-014 â€” Harden `hrdrClaudeNative.cmd`

## Goal

`hrdrClaudeNative.cmd` fails loudly when the Claude Code install fails, and it
handles paths that contain an apostrophe.

## Context

Formerly Icebox item IB-001. These are known minor gaps with no current impact: the
script works on the supported paths. The root `CLAUDE.md` says not to change this
script unless asked, so moving this task to `Backlog` counts as the asking.

## Acceptance criteria

- [x] The script checks the exit code of `npm install -g @anthropic-ai/claude-code`
      and stops with a clear message when it is non-zero.
- [x] The PowerShell `--cwd` and `--label` arguments are quoted so a path containing
      `'` works.

## Notes

## Log

- 2026-09-25: Migrated from the Icebox of Documentation/Planning/Backlog.md as IB-001, renumbered BL-014 so the board has one ID sequence.
- 2026-09-27: Deferred -> Backlog. Stewart asked for it to be done now (2026-09-27).
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. hrdrClaudeNative.cmd stops with npm's exit code when the Claude Code install fails, and reads paths and labels from the environment so an apostrophe no longer breaks them.
