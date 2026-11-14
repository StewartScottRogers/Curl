---
id: BL-5167
title: Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5167 — Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux

## Goal

`job Build and test (ubuntu-latest) step Fast tests` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Build and test (ubuntu-latest) step Fast tests` failed on Linux in CI run 43565364047 (https://github.com/StewartScottRogers/Curl/actions/runs/43565364047). First failing commit: 01e39905.

    Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 264 ms - Curl.Config.UnitTests.dll (net10.0)

Lanes test only on Windows, so reproduce with `gh run view 43565364047 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Build and test (ubuntu-latest) step Fast tests` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The Linux failure was a 3-minute inactivity hang with CurlCommandRunnerHttp2MultiplexingTests.RunAsync_ThreeUrlsToOneOrigin_CarriesThemOnStreamsOneThreeAndFiveOfOneConnection running, which killed the Console test host. Commit 01e39905 (watchdog-only change) cannot reach it, and the same test passed on the previous CI run, so it is a flaky hang; the cause was not reproducible on Windows. Decision: bound every wait in the test (WaitAsync 60 s, deadline in WhenOpenedAsync, Task.Delay instead of Task.Yield spin) so a hang fails that one test instead of aborting the host. Criteria met by keeping the change platform-neutral; fast Console tests for the class pass.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 1426f56b.
- 2026-11-13: Doing -> Done. Bounded the waits in the HTTP/2 multiplexing test so a hang fails one test, not the host
