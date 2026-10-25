---
id: BL-3492
title: Trace CI watch's silent no-ops in the running-run read and the cancel step
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-24
completed:
---
# BL-3492 — Trace CI watch's silent no-ops in the running-run read and the cancel step

## Goal

CI watch's two new steps, the early read of the running run (BL-3454) and the cancel of a run that lacks the CI fix (BL-3456, ADR-0811), say in the trace why they did nothing. The cancel step also decides each pair of runs only once, as its comment promises.

## Context

The shift-end review of 2026-10-24 (diff `808c067b4...31527f6ba`) found these. All are in `DarkFactory/CiWatch.ps1`:

- **The fetch is unchecked** (silent-failure-hunter). At about line 536, `git -C $Root fetch -q origin $Branch 2>&1 | Out-Null` has no exit-code check. If the fetch fails, the waiting run's commit is missing locally and `Get-CiFixTasksBetween` returns `$null`. `Stop-CiRunWithoutFix` then returns `''` with no trace, on every heartbeat.
- **`Get-CiRunningRun` fails silently** (silent-failure-hunter, about lines 409-425). These paths all return `$null` without a trace:
  - the `gh run view --json jobs` call failing;
  - the `catch { return $null }` around `ConvertFrom-Json`;
  - one job's log being unreadable, which also throws away the failures already read from the other failed jobs.

  The `Get-CiRuns` function next to it traces each failure with `Write-CiRunLine`.
- **An empty result reads as a git failure** (code-reviewer, about lines 446-448 and 470-473). `Get-CiFixTasksBetween` ends with `return @(...)`, which PowerShell unrolls, so an empty result reaches the caller as `$null`. `Stop-CiRunWithoutFix` therefore takes "no fix found" for "git failed". It returns before `$script:CiWatch.Logged[$decided] = 'looked'` is set, so `if (-not @($fixes).Count)` is never reached. The same `git diff` and `ls-tree` are repeated on every heartbeat for the same pair of runs.

## Acceptance criteria

- [ ] `Get-CiFixTasksBetween` returns an empty array, not `$null`, when git succeeds and no CI-fix task is found (for example with `return , @(...)`). It returns `$null` only when git fails, and that includes `ls-tree` failing.
- [ ] `Stop-CiRunWithoutFix` records the pair as decided when the commits compare and carry no fix. A `-TestCiWatch` case proves the second call does not run git again.
- [ ] A failed `git fetch` before the cancel step is traced, at most once per change of state, through `Write-CiRunLine` or the same change-only logging.
- [ ] Each `$null` return in `Get-CiRunningRun` is traced through `Write-CiRunLine -Key "running <id>"`. An unreadable job log skips only that job; the failures read from the other jobs are kept.
- [ ] `-TestCiWatch` passes.

## Notes

## Log

- 2026-10-24: Created.
