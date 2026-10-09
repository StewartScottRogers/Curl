---
id: BL-1859
title: Fix CI failure test1484 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1859 — Fix CI failure test1484 on Windows

## Goal

`test1484` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1484` failed on Windows in CI run 37907205689 (https://github.com/StewartScottRogers/Curl/actions/runs/37907205689). First failing commit: 85f6337b.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 37907205689 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test1484` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Not a defect of test1484: it passes in milliseconds locally, and in the failing run unlisted
  cases that take 1 ms elsewhere (test1072, 1073, 1480, 1488, 1489) took 10 to 15 s in the same
  window - a stall of the whole Windows runner held test1484 past the 30 s hang limit.
- Fix in `UpstreamConformanceTests`: a listed case whose attempt runs out of time (the runner's
  20 s curl limit or the test's 30 s case limit) runs once more before it is judged; a case that
  really hangs runs out of time twice and still fails. Chosen over raising the limits, which
  would only move the line a stall has to cross.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. A listed upstream case that runs out of time on a stalled CI runner runs once more before it is judged
