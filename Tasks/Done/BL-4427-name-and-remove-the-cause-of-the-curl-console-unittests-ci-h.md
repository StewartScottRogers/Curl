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
completed: 2026-11-02
---
# BL-4427 — Name and remove the cause of the Curl.Console.UnitTests CI hang (split from BL-4421)

## Goal

`Curl.Console.UnitTests` no longer hangs intermittently on the `Quality gates shard 3 of 3 (windows-latest)` CI job, and the cause is named.

## Context

Split from BL-4421 (its criterion 1). BL-4421 already landed `Curl.Console.UnitTests/HangBlame.runsettings` (commit 9ce35d213), which aborts a hung run after 3 minutes and names the running test via the blame collector. The hang happened twice on 2026-10-31 (CI runs 41217938101 and 41228308093), both times with `Skipped RemoveFile_DevNullOffWindows_IsNotRegularFile` as the last log line. It never reproduced locally in 36 runs; do not repeat that loop. Start from the log of the first `Quality gates shard 3 of 3` run that failed after 9ce35d213: `gh run list --workflow CI`, then `gh run view <id> --log`, searching for `Blame` and `The active test`.

## Acceptance criteria

- [x] What made `Curl.Console.UnitTests` hang in `Quality gates shard 3 of 3 (windows-latest)` is named under Notes and removed, with a focused unit test where the cause is in code.

## Notes

- Split from BL-4421. If no CI run has hung since 9ce35d213 there is nothing new to measure: read the log of any run after it for blame output, and if none exists, move this task to Blocked naming "a CI hang after 9ce35d213 to read" rather than looping locally.
- 2026-10-31 (lane 3): No CI run has hung since 9ce35d213 (19:47Z). The only completed work/dark-factory run containing it, 41236102295 (head ac0e3e86e), passed `Quality gates shard 3 of 3 (windows-latest)` (job 124640103761): Curl.Console.UnitTests ran 3137 tests in 13 s and Blame reported "All tests finished running, Sequence file will not be generated." Master run 41237470640 also passed. So there is no blame output naming a hung test yet; moved to Blocked as the note above says, waiting for a CI hang after 9ce35d213 to read. No code changed.
- 2026-11-02 (lane 2): Cause named. The first shard-3 hang after 9ce35d213 is CI run 41252530857 (job 124694071113, 2026-11-01 00:01Z). Blame aborted it after 3 minutes of inactivity in `Curl.Console.Cgr109Tests.RunAsync_SecondSameHostTransferWaitsPastMaxTime_TimesOutWithoutConnecting`. The last log line before it was `Skipped RemoveFile_DevNullOffWindows_IsNotRegularFile`, as in the two earlier hangs (41217938101, 41228308093). That skipped test is only the last one printed before the hung test, not the cause. The cause is in test code: `TimerFiringClock.Advance(2000)` fired only the timers due when it took its snapshot. A `-m` or poll timer that the run armed after the snapshot was never fired, so the run waited on a clock nobody moved.
- Removed by BL-4442 (59686eeba, `FireLateTimersUntilAsync` after `Advance`) and BL-4444 (62a300628, the same for the host-freed `-m` test). Both landed on 2026-11-01 at about 01:32Z and 01:44Z, after that run hung. The cause and fix are in the test's own clock, not in production code, so the focused test is the fixed Cgr109Tests itself.
- Evidence it no longer occurs: every CI run created since 2026-11-01T01:33Z was checked. `Quality gates shard 3 of 3 (windows-latest)` passed 62 times and failed 0 times. Five jobs were cancelled, each superseded by a newer push; their run times (15 s to 9 min) are within a normal job's, so none was hung. Three jobs were still running when checked. Locally, `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passed (3124 passed, 33 skipped, 0 failed, 6 s), and Blame reported "All tests finished running". This run changed no code, so it has no build or quality run to report beyond that test pass.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 68c218e4.
- 2026-10-31: Doing -> Blocked. a CI hang after 9ce35d213 to read: no Quality gates shard 3 run has hung since HangBlame.runsettings landed (run 41236102295 passed, blame found no hung test)
- 2026-11-02: Blocked -> Backlog. Stewart 2026-11-02: complete all tasks; stop waiting for a recurrence - reproduce the Curl.Console.UnitTests hang under load (repeat runs, parallel shards, CPU contention), then fix it, or close it with the evidence that it no longer occurs.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 68c218e4.
- 2026-11-02: Doing -> Done. Hang named (Cgr109Tests TimerFiringClock missed late timers, CI run 41252530857 blame), removed by BL-4442/BL-4444; 62 shard-3 passes since, none hung
