---
id: AF-0515
title: Root CLAUDE.md says the coordinator's CI watch ignores cancelled runs, but it files hangs from them
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:CLAUDE.md:Dark-factory-CI-watch:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0515 - Root CLAUDE.md says the coordinator's CI watch ignores cancelled runs, but it files hangs from them

## Summary

Medium finding from the truthfulness auditor at `CLAUDE.md:204`: Root CLAUDE.md says the coordinator's CI watch ignores cancelled runs, but it files hangs from them. Reported by an auditor flagged unreliable in 2026-10-31_1149.md.

## Evidence

Location: `CLAUDE.md:204`

The 'Dark factory' section says the coordinator 'reads the finished CI runs on the shift's branch (and the failed jobs of the one still running, ADR-0811) (cancelled ones ignored)'. DarkFactory/CiWatch.ps1:502-504 (Get-CiRuns) includes 'a cancelled run only when a job ran into its timeout-minutes (Get-CiHangs ...), whose hangs are its failures', and those are filed as High 'Fix CI hang in <job> (<assembly>)' tasks (CiWatch.ps1:1228 self-test). An agent reading CLAUDE.md would not expect a cancelled run to file a task. Phase 2: explained by ADR-1173 (BL-4420); CLAUDE.md was not updated with it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md, DarkFactory/CiWatch.ps1 -Pattern '\(cancelled ones ignored\)', 'a cancelled run only when a job ran into its timeout-minutes' | ForEach-Object { $_.Filename + ':' + $_.LineNumber }
```

- Expected: Only CiWatch.ps1:503 matches: CLAUDE.md says a cancelled run whose job timed out is read for hangs.
- Actual: CLAUDE.md:204 and CiWatch.ps1:503 both match.

## Re-audits

## Log

- 2026-10-31: filed proposed.
