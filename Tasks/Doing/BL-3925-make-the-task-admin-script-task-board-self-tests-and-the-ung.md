---
id: BL-3925
title: Make the task-admin script, task board self-tests and the unguarded hooks run under pwsh on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-admin.ps1, .claude/skills/task-board/Test-TaskBoardCriteria.ps1, .claude/skills/task-board/Test-TaskBoardDedupe.ps1, .claude/skills/task-board/Test-TaskBoardOverlap.ps1, .claude/hooks/whisper-milestone.ps1, .claude/hooks/format-cs.ps1]
requirement: none
created: 2026-10-27
completed:
---
# BL-3925 — Make the task-admin script, task board self-tests and the unguarded hooks run under pwsh on Linux and macOS

## Goal

`task-admin.ps1`, the three `Test-TaskBoard*.ps1` self-tests and the two hooks that are not guard
files (`whisper-milestone.ps1`, `format-cs.ps1`) run correctly under `pwsh` on Linux and macOS.

## Context

Part of BL-3934 (the whole repository workable on Linux and macOS). The board's main script,
`.claude/skills/task-board/task-board.ps1`, is a guard file and is handled in BL-3926 on the `audit`
branch; this task must not edit it. The files here call it with `powershell -NoProfile ...`, which
does not exist off Windows unless a shim is installed (BL-3932 installs one on the factory host, but
the scripts must not rely on that): pick `powershell` when `$IsWindows` or on Windows PowerShell
5.1, `pwsh` otherwise. `whisper-milestone.ps1` already keeps silent off Windows; keep that, and make
sure it exits 0 rather than throwing. Watch for backslash paths, `$env:LOCALAPPDATA` and
case-sensitive file names (the board's folders are `Backlog`, `Doing`, … exactly).

## Acceptance criteria

- [ ] No file in `touches` calls a bare `powershell`; each child PowerShell is chosen by platform as described in Context.
- [ ] `pwsh -File .claude/skills/task-board/Test-TaskBoardCriteria.ps1`, `Test-TaskBoardDedupe.ps1` and `Test-TaskBoardOverlap.ps1` pass on Windows and in WSL Ubuntu (or a Linux CI runner); results in the Log. If one fails only because of `task-board.ps1`, note it in BL-3926's Log instead of editing that file.
- [ ] `pwsh -File .claude/skills/task-board/task-admin.ps1 check` gives the same output on Linux as on Windows for the same board.
- [ ] Both hooks exit 0 under `pwsh` on Linux when fed a PostToolUse payload on stdin.
- [ ] Windows behaviour is unchanged, and `dotnet build` plus the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 45f8dc4b ed0cd9ed 90cedfcc e1b07e9b 677155f8.
