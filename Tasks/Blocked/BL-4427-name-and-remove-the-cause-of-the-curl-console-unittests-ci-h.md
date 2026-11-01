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
- 2026-10-31 (lane 3): No CI run has hung since 9ce35d213 (19:47Z). The only completed work/dark-factory run containing it, 41236102295 (head ac0e3e86e), passed `Quality gates shard 3 of 3 (windows-latest)` (job 124640103761): Curl.Console.UnitTests ran 3137 tests in 13 s and Blame reported "All tests finished running, Sequence file will not be generated." Master run 41237470640 also passed. So there is no blame output naming a hung test yet; moved to Blocked as the note above says, waiting for a CI hang after 9ce35d213 to read. No code changed.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 68c218e4.
- 2026-10-31: Doing -> Blocked. a CI hang after 9ce35d213 to read: no Quality gates shard 3 run has hung since HangBlame.runsettings landed (run 41236102295 passed, blame found no hung test)
