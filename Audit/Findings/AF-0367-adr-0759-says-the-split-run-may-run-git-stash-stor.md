---
id: AF-0367
title: ADR-0759 says the split run may run git stash store; $SplitForbidden denies it
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-23_2322.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-23_0618.md, 2026-10-23_2322.md).
key: truthfulness:Documentation/Planning/Decisions/ADR-0759-a-task-over-its-cost-cap-is-split-by-one-bounded-run.md:SplitForbidden:stale-adr
reproduction: none
task: BL-3220
tasks: BL-3220
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed: 2026-10-23
closed-how: consecutive
closed-by: 2026-10-23_0618.md, 2026-10-23_2322.md
---
# AF-0367 - ADR-0759 says the split run may run git stash store; $SplitForbidden denies it

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0759-a-task-over-its-cost-cap-is-split-by-one-bounded-run.md:21`: ADR-0759 says the split run may run git stash store; $SplitForbidden denies it.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0759-a-task-over-its-cost-cap-is-split-by-one-bounded-run.md:21`

ADR-0759's Decision, lines 20-21: `$SplitForbidden`, "which denies every git command a lane may not run and every `git stash` subcommand except `list` and `store`". The code, DarkFactory/Prompts.ps1:434-435: $SplitForbidden = $Forbidden + @(..., 'git stash push', 'git stash save', 'git stash apply', 'git stash pop', 'git stash drop', 'git stash clear', 'git stash create', 'git stash branch', 'git stash store'). So 'git stash store' is denied. DarkFactory/TaskStash.ps1:451 even self-tests 'the split run may never run git stash store'. The ADR states a permission the split run does not have. An agent reading it would write a split prompt or tooling that relies on `git stash store` working.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0759-*.md, DarkFactory/Prompts.ps1 -Pattern 'except `list` and `store`', "'git stash store'" -SimpleMatch | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only one side matches: the ADR names only `list` as allowed, consistent with Prompts.ps1:435 denying 'git stash store'. Or Prompts.ps1 does not deny it.
- Actual: ADR-0759-a-task-over-its-cost-cap-is-split-by-one-bounded-run.md:21 and Prompts.ps1:435: the ADR allows store and $SplitForbidden denies it.

## Re-audits

- 2026-10-23 | 2026-10-23_0618.md | reproduces: no | Ran the reproduction: the only match is Prompts.ps1:435 ($SplitForbidden's 'git stash store'). ADR-0759 no longer contains 'except `list` and `store`'; its line 21 now says the split run is denied 'every git stash subcommand except list', which agrees with $SplitForbidden.
- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Ran the reproduction: only Prompts.ps1:435 ('git stash store' in $SplitForbidden) matched. ADR-0759 no longer contains 'except `list` and `store`'; its line 21 now says the split run is denied 'every `git stash` subcommand except `list`', which matches the code.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-23: accepted -> closed. Re-audit 2026-10-23_2322.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-23_0618.md, 2026-10-23_2322.md).
