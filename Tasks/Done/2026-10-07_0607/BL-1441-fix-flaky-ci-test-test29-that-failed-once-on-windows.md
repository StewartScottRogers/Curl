---
id: BL-1441
title: Fix flaky CI test test29 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-04
completed: 2026-10-04
---
# BL-1441 — Fix flaky CI test test29 that failed once on Windows

## Goal

`test29` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `test29` failed on Windows in CI run 37187531762 (https://github.com/StewartScottRogers/Curl/actions/runs/37187531762). First failing commit: 95363aaa.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 37187531762 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `test29` fail intermittently is named under Notes and removed.
- [x] `test29` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: in CI run 37187531762 test29 took 12 s and exited 0 - curl's `-m 2` never fired before
the sws emulation closed the connection at the end of `<postcmd>` `wait 10`. The thread pool
was starved (a loaded Windows runner): curl's MaxTimeWatchdog callback and the server's 10 s
`Task.Delay` continuation both ran only when it recovered, in no set order. BL-1321 held the
close back 250 ms only when the server woke more than 1 s past its moment, but a stall from
about 1.9 s to 10.4 s wakes the close just 0.4 s late beside a `-m` 8.4 s overdue, so the close
went first and curl finished with exit 0.

Fix (`SwsHttpServerConnection.WaitUntilAsync`): the 250 ms settle now also follows every wait
longer than one second, whether it woke on time or not, so any curl timer that fell due inside
it gets its turn before the close. A wait of a second or less is too short for a stall inside it
to reorder the timers a case races. Cost: 250 ms per long `<postcmd>` wait or long writedelay in
real-clock cases only; the whole conformance run still takes about 6 s.

Tests: `PostcmdWait_LongerThanASecondAndWokenOnTime_*` (a 10 s wait woken on time is held for an
overdue cancellation, and closes 250 ms later), `PostcmdWait_OfOneSecondWokenSecondsLate_*` (the
late-wake path alone), and the three tests that pinned a close at exactly a 2 or 3 s wait now
include the settle time. Measure-CodeQuality on Curl.Conformance.UnitLibrary: 0 failing members.
Fast suite green locally; the CI result for the landing commit is the shift's CI watch's to read
(lanes cannot see it before integration), and a recurrence is filed again by it.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. test29's -m 2 now always beats sws's 10 s postcmd close: every wait over a second settles 250 ms so curl timers starved inside it go first; CI on the landing commit is left to the shift's CI watch
