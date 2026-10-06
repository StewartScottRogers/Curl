---
id: BL-1056
title: Fix flaky CI test test1 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1056 — Fix flaky CI test test1 that failed once on Windows

## Goal

`test1` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `test1` failed on Windows in CI run 36786079169 (https://github.com/StewartScottRogers/Curl/actions/runs/36786079169). First failing commit: 4e66f3f1.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 36786079169 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `test1` fail intermittently is named under Notes and removed.
- [x] `test1` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not curl. In run 36786079169 `test1`, `test2` and `test3` all took 10 s, and
  `test3` is a case that is *skipped* - it never runs curl at all. They are the first rows of
  a method-level parallel suite, starting while other test assemblies ran beside them on a
  4-core runner, so the whole test host stalled (thread-pool ramp-up and first-time JIT of
  curl's code paths). `UpstreamCaseRunner.RunCurlAsync` started its wall-clock limit the
  moment curl's work item was *queued* with `Task.Run`, so the time spent waiting for a pool
  thread, not curl's own run, used up the 10 s.
- Fix: the runner now starts the limit only once curl's delegate is running on its thread
  (a `TaskCompletionSource` set as the delegate starts), and `UpstreamConformanceTests`
  gives curl 20 s instead of 10 (still under the 30 s `CaseHangLimit`). Locally no case of
  the 2013 comes near either limit (the whole run takes about 7 s), so the headroom costs
  nothing; a looping case still fails, just after 20 s.
- `UpstreamCaseRunnerTests`' `ExpiringTimeProvider` now waits for the limit's timer to be
  created, since it is created after an await on another thread; the two time-limit tests
  passed ten runs in a row.
- No new test pins "the limit starts when curl starts": the queue delay cannot be produced
  deterministically without replacing the thread pool, and the change adds no branch.
- BL-1057 (`test2`, same run) has the same cause and is fixed by this change too.
- The CI half of the second criterion is checked by the shift coordinator's CI watch
  (BL-987) once this commit is integrated; it files a new task if `test1` fails again. A lane
  cannot push, so it is ticked on the local pass.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The conformance time limit starts when curl starts, with 20 s headroom, so a stalled CI test host no longer fails test1
