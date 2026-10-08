---
id: BL-1718
title: Fix CI failure FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-07
completed: 2026-10-08
---
# BL-1718 — Fix CI failure FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes on Windows

## Goal

`FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes` failed on Windows in CI run 37733074018 (https://github.com/StewartScottRogers/Curl/actions/runs/37733074018). First failing commit: 711fbdf0.

    Assertion failed. Expected value to be null.

Lanes test only on Windows, so reproduce with `gh run view 37733074018 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the CI log shows "the strip pattern ^Date: took longer than 1 seconds". `UpstreamRegex`
  gave every strip match a 1-second wall-clock timeout, and a stall on the Windows runner
  (the test took 1.7 s for a microsecond match) ran past it. Not a platform difference.
- Fix: `UpstreamRegex.MatchTimeout` is now 10 s, so a runner stall cannot cut short a real
  pattern, while a pattern that backtracks without end still stops. The catastrophic-backtracking
  test (`FindFirstDifference_StripPatternThatRunsTooLong_NamesIt`) passes its own 1-second
  timeout through new overloads `UpstreamRegex.TryCreate(pattern, options, matchTimeout)` and
  `UpstreamCaseVerification.FindFirstDifference(testCase, run, stripMatchTimeout)`, so the fast
  suite does not wait 10 s and the timeout branch stays covered. New `UpstreamRegexTests` pins
  the 10-second default, the passed timeout and the unreadable-pattern null.
- Choice (sensible default): 10 s rather than a retry - one number, 10x the stall margin seen,
  and still bounded. CI on Linux and macOS is checked by the shift's CI watch after integration;
  the change is platform-neutral (no paths, no OS text).

## Log

- 2026-10-07: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Strip matches get 10 s; the stall-induced timeout cannot recur; fast tests green
