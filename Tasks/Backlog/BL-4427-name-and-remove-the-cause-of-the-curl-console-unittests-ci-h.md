---
id: BL-4427
title: Name and remove the cause of the Curl.Console.UnitTests CI hang (split from BL-4421)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-31
completed:
---
# BL-4427 — Name and remove the cause of the Curl.Console.UnitTests CI hang (split from BL-4421)

## Goal

`Curl.Console.UnitTests` no longer hangs intermittently on the `Quality gates shard 3 of 3 (windows-latest)` CI job, and the cause is named.

## Context

Split from BL-4421 (its criterion 1). BL-4421 already landed `Curl.Console.UnitTests/HangBlame.runsettings` (commit 9ce35d213), which aborts a hung run after 3 minutes and names the running test via the blame collector. The hang happened twice on 2026-10-31 (CI runs 41217938101 and 41228308093), both times with `Skipped RemoveFile_DevNullOffWindows_IsNotRegularFile` as the last log line. It never reproduced locally in 36 runs; do not repeat that loop. Start from the log of the first `Quality gates shard 3 of 3` run that failed after 9ce35d213: `gh run list --workflow CI`, then `gh run view <id> --log`, searching for `Blame` and `The active test`.

## Acceptance criteria

- [ ] What made `Curl.Console.UnitTests` hang in `Quality gates shard 3 of 3 (windows-latest)` is named under Notes and removed, with a focused unit test where the cause is in code.

## Notes

- Split from BL-4421. If no CI run has hung since 9ce35d213 there is nothing new to measure: read the log of any run after it for blame output, and if none exists, move this task to Blocked naming "a CI hang after 9ce35d213 to read" rather than looping locally.

## Log

- 2026-10-31: Created.
