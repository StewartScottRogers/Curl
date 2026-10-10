---
id: AF-0149
title: CI red 30.42 min after a lane changed guard file task-board.ps1 on work/dark-factory (run 37931431648)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:CI-run-37931431648:ci-red
reproduction: none
task: BL-1968
tasks: BL-1968
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0149 - CI red 30.42 min after a lane changed guard file task-board.ps1 on work/dark-factory (run 37931431648)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 30.42 min after a lane changed guard file task-board.ps1 on work/dark-factory (run 37931431648).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-09T12:45:17Z, end 13:15:42Z, 30.42 min, runId 37931431648, failure on ea846a2e (BL-1879). The failing job was audit-guard: BL-1876 had broad touches [.claude] and edited .claude/skills/task-board/task-board.ps1. The CI watch filed BL-1882 at 05:46:00 PDT (commit 575b6524b), under a minute after the red. Run 37933508564 on c0b0a8cc failed again. The fix took two commits: bfa5bbc01 (06:08:27 PDT) wrote the file empty ('a shell path rewrite broke the git show argument'), and 45d1dc049 (06:08:37) restored it. Run 37934762067 turned green at 13:15:42Z. f38a77c90 (06:22 PDT) then stopped broad touches reaching guard files. No merge to master during the spell: PR #92 merged at 06:46 PDT, after the green run.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 37931431648
```

- Expected: No red spell over 30 minutes
- Actual: start 2026-10-09T12:45:17Z end 13:15:42Z minutes 30.42 runId 37931431648

## Re-audits

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
