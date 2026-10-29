---
id: BL-3926
title: Make the guard files run under pwsh on Linux and macOS (task-board.ps1, guard hook, settings.json hooks, audit guard)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1, .claude/hooks/guard-audit-paths.ps1, .claude/settings.json, Audit/Guard]
lane: no
requirement: none
created: 2026-10-27
completed: 2026-10-29
---
# BL-3926 — Make the guard files run under pwsh on Linux and macOS (task-board.ps1, guard hook, settings.json hooks, audit guard)

## Goal

The guard files work on Linux and macOS: `task-board.ps1` runs under `pwsh`, the PreToolUse guard
hook still refuses a lane's reach into audit paths there, the hooks in `.claude/settings.json` start
on any platform, and CI's audit guard runs on a Linux runner.

## Context

Part of BL-3934 (the whole repository workable on Linux and macOS). Interactive only: these are
guard files (root `CLAUDE.md`, "Audit office"), so the work is done on the `audit` branch (or a
branch from `master` for guard work) and merged under the standing exception once CI is green on
all three platforms. Files: `.claude/skills/task-board/task-board.ps1` (calls `powershell` for the
whisper hook and builds paths), `.claude/hooks/guard-audit-paths.ps1` (path matching must stay
case-insensitive and accept `/` separators), `.claude/settings.json` (its three hook commands start
`powershell -NoProfile ...`, which fails off Windows; pick a form that works on both, e.g. `pwsh`
where Windows has it too, or a wrapper that tries one then the other, and say which in an ADR), and
`Audit/Guard/Test-AuditPathsUntouched.ps1` with its `-SelfTest`. If a change to
`.github/workflows/ci.yml` is needed, it belongs here too, since that is also a guard file.

## Acceptance criteria

- [x] `task-board.ps1 status`, `next`, `capacity`, `hotspots`, `new` and `move` give the same results under `pwsh` on Linux as on Windows for a scratch copy of the board (commands and output summary in the Log).
- [x] Under `pwsh` on Linux with `CURL_DARK_FACTORY_LANE` set, `guard-audit-paths.ps1` refuses a payload that reads `Audit/README.md` and allows one that reads `README.md`.
- [x] Every hook command in `.claude/settings.json` starts on Windows and on Linux, and an ADR marked "Decided by Claude under Stewart's delegation" records the form chosen.
- [x] `pwsh -File Audit/Guard/Test-AuditPathsUntouched.ps1 -SelfTest` passes on Linux and Windows.
- [x] The change reached `master` through the audit-branch pull request with CI green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: cfdfcaf2 1ae8d327 10b7d122 52dc155f d0e9ac37.
- 2026-10-29: Linux checks run under pwsh 7.4.6 in WSL Ubuntu (invariant globalization), Windows under pwsh 7. task-board.ps1 status, next, capacity, hotspots -Min 3, new, move (Blocked, Backlog, Deferred, Doing) on a scratch copy of the live board: all 12 outputs and the moved task file byte-identical on both. guard-audit-paths.ps1 with CURL_DARK_FACTORY_LANE=1: Read Audit/README.md exit 2, audit/readme.md exit 2, Read README.md exit 0, Edit .claude/settings.json exit 2, unparseable input exit 2; without the variable exit 0. Test-AuditPathsUntouched.ps1 -SelfTest: all 18 cases PASS on Linux and Windows.
- 2026-10-29: Hooks: Claude Code runs hook commands with sh (Git Bash on Windows, measured). settings.json's three commands are now sh lines running powershell or else pwsh, the guard failing closed with neither (ADR-1015). New Audit/Guard/Test-GuardHooks.ps1: 7/7 PASS on Windows and Linux; CI job guard-hooks runs it on all three platforms. PR #252.
- 2026-10-29: PR #252 merged to master (98e12cbb1) with CI green on Windows, Linux and macOS, guard-hooks included; master merged into work/dark-factory.
- 2026-10-29: Doing -> Done. The hooks start under powershell or pwsh on Windows, Linux and macOS, the audit guard fails closed, and CI's guard-hooks job proves it on all three
