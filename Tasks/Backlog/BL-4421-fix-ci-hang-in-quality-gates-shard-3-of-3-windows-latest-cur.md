---
id: BL-4421
title: Fix CI hang in Quality gates shard 3 of 3 (windows-latest) (Curl.Console.UnitTests)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-31
completed:
---
# BL-4421 — Fix CI hang in Quality gates shard 3 of 3 (windows-latest) (Curl.Console.UnitTests)

## Goal

`Quality gates shard 3 of 3 (windows-latest)` finishes well inside its `timeout-minutes` on every `CI` run; in CI run 41228308093 it hung while `Curl.Console.UnitTests` was running.

## Context

Filed by the dark factory's CI watch (BL-4420). In CI run 41228308093 (https://github.com/StewartScottRogers/Curl/actions/runs/41228308093) on commit 23bacbc6, job `Quality gates shard 3 of 3 (windows-latest)` ran into its `timeout-minutes` and GitHub cancelled it, so the run reads as cancelled, not failed. The last test assembly it started was `Curl.Console.UnitTests`.

    Last log line at 2026-10-31 18:33:26 UTC: Skipped RemoveFile_DevNullOffWindows_IsNotRegularFile - then nothing for 39 min until ##[error]The operation was canceled.

A hang can be intermittent: the next run may pass. Reproduce with `dotnet test` on that project and `--blame-hang-timeout 3m`, and read the job's whole log with `gh run view 41228308093 --log`.

## Acceptance criteria

- [ ] What made `Curl.Console.UnitTests` hang in `Quality gates shard 3 of 3 (windows-latest)` is named under Notes and removed.
- [ ] `Quality gates shard 3 of 3 (windows-latest)` finishes inside its `timeout-minutes` in the `CI` workflow for the commit that lands the fix.

## Notes

- 2026-10-31, lane 1, first claim. **Measured:** the same hang happened twice that day, in
  run 41217938101 (fe634e2ce, 16:16 UTC) and run 41228308093, and both times the last line is
  `Skipped RemoveFile_DevNullOffWindows_IsNotRegularFile`. Nothing in Curl's code changed
  between the last green run 41227713497 (c3f18a294) and 23bacbc6c: only DarkFactory scripts
  and task files did. So the hang is intermittent in code that is already there. In the green
  run, the project printed `Passed!` 2 s after that same skipped line. That puts the hang in a
  test still running at the end of the run, or in the test host's exit, not in the tests
  near it in the listing: tests run method-level parallel (`MSTestSettings.cs`, Workers = 0),
  so listing order says little. Locally it never hung: 36 runs, all with
  `--collect "Code Coverage;Format=cobertura"` as CI does it, and three with stdin held open
  (15 at the default worker count, 6 at `MSTest.Parallelize.Workers=2`, 6 at 4, plus single
  runs). Each passed in 4 to 6 s. I ruled out a read of the real stdin: `Program.Main`'s
  tests open it, but the refused command line never reads it, and the runs with stdin held
  open did not hang.
- **Done (complete in itself, commit 9ce35d213):** `Curl.Console.UnitTests/HangBlame.runsettings`, selected by
  the project's `RunSettingsFilePath`, runs every `dotnet test` of this project under the
  blame collector with a 3-minute hang timeout and no dump. With `--collect "Code Coverage"`
  it reports `Data collector 'Blame' message: All tests finished running` and all 3104 tests
  pass. On the next hang, CI's quality shard aborts the run after 3 minutes instead of
  waiting 39 minutes for `timeout-minutes`, and the blame logger names the test that was
  running. `Measure-CodeQuality.ps1` counts `Test Run Aborted` as a real failure, so the job
  fails red with that name in its log. Decided without an ADR because it changes no behaviour
  of Curl, only how the tests run. No production code changed, so I did not run
  Measure-CodeQuality. The fast tests I ran were this project's only, since the change is
  limited to it.
- **Left:** naming and removing the cause (criterion 1). The next claim should read the log
  of the first CI run of `Quality gates shard 3 of 3` that fails after this commit (search it
  for `Blame` and `The active test`). Then fix that test, or the code under it, and finish.
  If no hang recurs, there is nothing new to measure: do not re-run the local loop above.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 3fca587f 2911d5f5.
- 2026-10-31: Doing -> Backlog. Cause not yet named: no local repro in 36 runs; the project now runs under a 3-minute blame hang timeout so CI's next hang names the stuck test (see Notes)
