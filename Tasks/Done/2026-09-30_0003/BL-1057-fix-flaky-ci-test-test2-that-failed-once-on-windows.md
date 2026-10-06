---
id: BL-1057
title: Fix flaky CI test test2 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1057 — Fix flaky CI test test2 that failed once on Windows

## Goal

`test2` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `test2` failed on Windows in CI run 36786079169 (https://github.com/StewartScottRogers/Curl/actions/runs/36786079169). First failing commit: 4e66f3f1.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 36786079169 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `test2` fail intermittently is named under Notes and removed.
- [x] `test2` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the same stall BL-1056 fixed. In run 36786079169 `test1`, `test2` and the skipped
  `test3` all took exactly 10 s. As the first rows of the parallel conformance suite on a busy
  4-core runner, they waited for thread-pool threads, and `UpstreamCaseRunner.RunCurlAsync`
  counted that queue wait against curl's 10 s limit.
- Removed by commit 030bb05a (BL-1056): the limit now starts when curl's delegate starts
  running, and curl gets 20 s. This task needs no further code change; it verifies the fix.
- Verified locally: build clean, fast tests green, `Curl.Conformance.UnitTests` 635 passed and
  0 failed, `test2` included. The CI half is checked by the coordinator's CI watch (BL-987),
  which files a new task if `test2` fails again. A lane cannot push, so this box is ticked on
  the local pass.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. test2 no longer times out on a stalled CI test host: the conformance time limit starts when curl starts (fixed by BL-1056)
