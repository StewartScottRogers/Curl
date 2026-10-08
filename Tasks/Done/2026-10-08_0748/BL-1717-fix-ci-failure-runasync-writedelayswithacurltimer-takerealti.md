---
id: BL-1717
title: Fix CI failure RunAsync_WriteDelaysWithACurlTimer_TakeRealTime on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-07
completed: 2026-10-08
---
# BL-1717 — Fix CI failure RunAsync_WriteDelaysWithACurlTimer_TakeRealTime on Windows

## Goal

`RunAsync_WriteDelaysWithACurlTimer_TakeRealTime` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_WriteDelaysWithACurlTimer_TakeRealTime` failed on Windows in CI run 37733074018 (https://github.com/StewartScottRogers/Curl/actions/runs/37733074018). First failing commit: 711fbdf0.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37733074018 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_WriteDelaysWithACurlTimer_TakeRealTime` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix. (Local pass verified; CI is checked by the shift's CI watch on the integrated commit.)

## Notes

- Cause: the Windows runner stalled the whole test process for over ten seconds. The case
  needs about 600 ms of real time (25 reply bytes in two writes, 300 ms after each), yet the
  test took 14.5 s and hit the runner's 10 s limit; in the same run
  `FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes`
  saw `^Date:` take over its 1 s regex timeout (filed separately as BL-1718). Nothing in the
  emulation or the runner is wrong.
- Fix: this test, the only one in `UpstreamCaseRunnerTests` on the real clock, runs under a
  two-minute limit (`RealClockTimeLimit`) instead of the shared ten seconds. The test pins that
  the waits take at least 300 ms of real time, not a ceiling, so a longer limit loses nothing.
  The library's own limit for real cases is untouched.

## Log

- 2026-10-07: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Real-clock runner test given a two-minute limit so a CI runner stall cannot fail it
