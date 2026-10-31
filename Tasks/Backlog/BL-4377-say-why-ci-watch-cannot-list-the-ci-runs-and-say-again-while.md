---
id: BL-4377
title: Say why CI watch cannot list the CI runs, and say again while it still cannot
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-30
completed:
# BL-4377 — Say why CI watch cannot list the CI runs, and say again while it still cannot

## Goal

When the coordinator's CI watch cannot list the `CI` runs, its log line carries gh's own error text, and a watch that stays unable to list says so again (with how long), so a blind watch is told apart from a one-off failure at shift start.

## Context

`Get-CiRuns` (`DarkFactory/CiWatch.ps1`, near line 400) runs `gh run list --workflow CI --branch <b> ...` with `2>$null` and, on a non-zero exit, writes `cannot list the CI runs on <branch> with gh` through `Write-CiRunLine -Key 'list'`, which traces each key once per shift. Every shift since 15:22 on 2026-10-30 (DarkFactory-20261030-152226, -193026, -193800, -205631, -205849) logged that line once, on its first heartbeat, and never again; the 06:06 shift logged 68 other `ci` lines. The same command run by hand in Windows PowerShell at 21:25 exited 0. So either the first heartbeat's call fails for a reason no one can see (gh's stderr is discarded), or the watch stays blind for the whole shift and the once-per-key trace hides it. A CI failure on dbc468ecd (run 41151657472, test670 on Windows) was not filed by the shift that was running (BL-4378 files it by hand).

## Acceptance criteria

- [ ] The `cannot list` line includes gh's exit code and the first line of its stderr.
- [ ] While listing keeps failing, the line is written again at most every 30 minutes with how long the watch has been unable to list; the first successful list after a failure writes one `listing CI runs again` line.
- [ ] `RunDarkFactory.ps1`'s CI-watch self-test (`-TestCiWatch` or the area's own switch) covers the three cases with stand-in `-ListRuns` blocks.
- [ ] The cause of the first-heartbeat failure is named in Notes from a shift's log after this change, and fixed if it is the factory's own (for example a call made before the shift's environment is ready).

## Notes

## Log

- 2026-10-30: Created.
