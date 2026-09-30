---
id: BL-1031
title: Find why the CI watch filed nothing while the TcpDialerTests failures kept CI red for hours
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1031 — Find why the CI watch filed nothing while the TcpDialerTests failures kept CI red for hours

## Goal

When a CI run on the shift's branch fails, the running shift's coordinator files a High task for each failing test within one heartbeat, as BL-987 intended, and logs that it did.

## Context

Shift 20260929-191003 ran with BL-987's `Invoke-CiWatch` in its coordinator loop (`RunDarkFactory.ps1`, called beside `Publish-BoardStatusIfDue`). From 04:24 UTC on 2026-09-30 every `CI` run on `work/dark-factory` failed on ubuntu-latest with `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` and `BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed` (e.g. run 36674691490), yet the coordinator log `Curl.logs/DarkFactory-20260929-191003.log` has no CI watch line at all and no task was filed; the shift's end logged `merge   not merged: CI failure on bfd523f`. An interactive session found and fixed the failure (BL-1030). `-TestCiWatch` passes on its recorded logs, so the fault is in the live path: reading `gh run list`/`gh run view --log-failed`, the run selection, a silent catch, or the loop never reaching the call.

## Acceptance criteria

- [x] The cause is found and stated under Notes.
- [x] `Invoke-CiWatch` writes one coordinator log line each time it examines a finished run (run id, conclusion, what it filed or why it filed nothing).
- [x] Replayed against run 36674691490 (or a recorded copy of its `--log-failed` output), the watch files one task per failing test.
- [x] `-TestCiWatch` covers the case that failed live, and every `-Test*` switch prints no FAIL.

## Notes

**Cause - two faults, found by running the watch's own functions against the live `gh` output.**

1. *Timing: the watch was never running when the failures finished.* The Context's "from 04:24 UTC" is off: run 36668685198 at 04:24 UTC passed; the first red run was 36671448420 on bfd523f, created 05:00 UTC and finished 05:05:30 UTC (22:05:30 local). Shift 20260929-191003's coordinator loop had already stopped at 22:00:54 (every lane ended on the weekly token limit), and `Invoke-CiWatch` is only called from that loop. The shift-end merge then waited for that very run and logged `not merged: CI failure on bfd523f`, but nothing read its failures. The next shift (20260929-220544) spent 22:05-06:00 in `Wait-ForFreshSession`, which did not watch CI either; by 06:00 CI had been green since 06:10 UTC (BL-1030's fix), so there was nothing left to file.
2. *A run with exactly one failing test was dropped silently.* In `Get-CiRuns`, `$failures = if (...) { @(...) } else { @() }` unrolls the if statement's output, so a single failure came back as a bare `[pscustomobject]`, and Windows PowerShell 5.1 gives a bare object no `Count`; `-not $failures.Count` was true and the run was skipped as if its log named no failure. Live proof: run 36690174792 (b86b15d, one macOS failure of `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext`) was missing from `Get-CiRuns`' result, which is why today's shift logged no CI line either. Two-failure runs such as 36674691490 were unaffected. `-TestCiWatch` never caught it because its verdict tests build run objects by hand and never went through `Get-CiRuns`.

**Fixes (all in `RunDarkFactory.ps1`).**
- `Get-CiRuns` assigns `$failures` apart from the if, and takes `-ListRuns`/`-ReadLog` script blocks (defaulting to `gh`) so `-TestCiWatch` drives the live path on recorded output; it now returns an empty list as a list, not `$null`.
- New `Write-CiRunLine` and `Get-CiRunOutcome`: the coordinator traces `ci run <id> success on <sha>: nothing to file`, or per failure `regression, filed BL-###` / `covered by a task` / `filed with run <newer>` / `on Linux only, waiting for the next run or HH:mm to confirm it` / `passed since, not filed`; plus lines for a failed run whose log names no failure, an unreadable log, and gh failing to list runs. Filing itself moved unchanged into `New-CiFailureTasks`.
- The coordinator watches CI while it waits for a fresh session before its shift (`Wait-ForNewSession`, coordinator only, via `$script:CiWatchBranch`), and once more after the shift-end merge (`Invoke-CiWatch -Final`), before the ci-watch worktree is removed.

**Choices made (sensible defaults, rule 1).**
- "One line each time it examines a finished run" is taken as one line per run per outcome: the watch re-reads the same six runs every heartbeat, and repeating identical lines every 3 minutes (about 120 an hour) would bury the log. A line is written the first time a run is examined and again whenever what the watch made of it changes (waiting -> filed).
- `-Final` confirms a one-platform, newest-run-only failure at once (ConfirmMinutes 0) instead of waiting 30 minutes: the shift is ending, the merge has just been refused on that run, and without `-Continuous` no later process would ever read the next run. The cost is that a one-off flake at a shift's very end is filed as a regression rather than as flaky; the task still names the run, so whoever fixes it sees that.
- The replay criterion is proven at the verdict level (one verdict per failing test is exactly one `task-board.ps1 new` each); the filing step itself pushes to origin and is not run offline.

**Verification.** `-TestCiWatch`: 25 PASS, 0 FAIL, including the recorded 36674691490 replay (waits at +5 min, two regressions at +31 min, two at once under `-Final`), the recorded single-failure run 36690174792 (fails on the old code: checked by reverting the one line), and the run tracing. `-TestLaneMarker`, `-TestAutoLanes`, `-TestMachineProbe`, `-TestOutOfTokens`, `-TestTaskIds`, `-TestFlakyTests`, `-TestShiftBranch`, `-TestHeartbeat`, `-TestRestart`: exit 0, no FAIL. `-TestAlarm` not run (it sounds the audible alarm; it touches nothing changed here). `dotnet build` clean; fast tests green.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The CI watch keeps runs with a single failing test, traces every run it examines, and also watches during the pre-shift token wait and after the shift-end merge
