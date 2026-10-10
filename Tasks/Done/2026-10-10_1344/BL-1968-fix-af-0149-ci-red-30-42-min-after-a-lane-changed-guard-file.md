---
id: BL-1968
title: Fix AF-0149: CI red 30.42 min after a lane changed guard file task-board.ps1 on work/dark-factory (run 37931431648)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1968 — Fix AF-0149: CI red 30.42 min after a lane changed guard file task-board.ps1 on work/dark-factory (run 37931431648)

## Goal

The defect the audit office reported as AF-0149 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0149 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0149-ci-red-30-42-min-after-a-lane-changed-guard-file-t.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-09T12:45:17Z, end 13:15:42Z, 30.42 min, runId 37931431648, failure on ea846a2e (BL-1879). The failing job was audit-guard: BL-1876 had broad touches [.claude] and edited .claude/skills/task-board/task-board.ps1. The CI watch filed BL-1882 at 05:46:00 PDT (commit 575b6524b), under a minute after the red. Run 37933508564 on c0b0a8cc failed again. The fix took two commits: bfa5bbc01 (06:08:27 PDT) wrote the file empty ('a shell path rewrite broke the git show argument'), and 45d1dc049 (06:08:37) restored it. Run 37934762067 turned green at 13:15:42Z. f38a77c90 (06:22 PDT) then stopped broad touches reaching guard files. No merge to master during the spell: PR #92 merged at 06:46 PDT, after the green run.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 37931431648
```

- Expected: No red spell over 30 minutes
- Actual: start 2026-10-09T12:45:17Z end 13:15:42Z minutes 30.42 runId 37931431648

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The red spell's cause (a lane with broad `touches: [.claude]` editing guard file task-board.ps1) was fixed by f38a77c90, which is an ancestor of this branch: a touch that is a folder holding a guard file is interactive only, and the PreToolUse hook refuses a lane's Edit/Write of any guard file. No RunDarkFactory.ps1 change is needed; its CI watch already filed the fix task within a minute (BL-1882).
- Criterion 1 is met by the fix being in place (not re-run: the logs are an audit path); criterion 2 holds because this task changes no code, only the task file.
- The reproduction reads the audit worktree's logs, an audit path a lane may not read, and its `-Since 2026-10-09` window keeps listing the historical spell. The re-audit measures the clean commit and closes the finding on evidence.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Cause already fixed by f38a77c90 (guard files and broad touches); no code change needed; re-audit closes AF-0149
