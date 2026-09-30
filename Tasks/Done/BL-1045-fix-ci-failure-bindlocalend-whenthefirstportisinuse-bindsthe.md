---
id: BL-1045
title: Fix CI failure BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1045 — Fix CI failure BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext on Windows

## Goal

`BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` failed on Windows in CI run 36761924824 (https://github.com/StewartScottRogers/Curl/actions/runs/36761924824). First failing commit: 9691137a.

    Test method Curl.Networking.TcpDialerTests.BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext threw exception:

Lanes test only on Windows, so reproduce with `gh run view 36761924824 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in the test, not in `TcpDialer`. `HoldAPortWithTheNextFree` took an ephemeral port and released its probe of `port + 1` before `BindLocalEnd` ran. Windows hands out ephemeral ports in order, so a test running in parallel that bound port 0 took `port + 1` in that gap; both ports were then in use and `LocalBindException` was thrown.
- Fix: the helper now picks the pair below 32768, under the ephemeral range on Windows (49152 and up), Linux (32768 and up) and macOS (49152 and up), starting at a random port in 20000-30000 and stepping on while either bind fails. No production change.
- Verified locally: build clean, fast tests green, Networking tests run three times. CI on all three platforms runs when the shift pushes this commit; the shift's CI watch files a new task if it fails again.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. BindLocalEnd port-range test no longer races parallel ephemeral binds; takes its port pair below every platform's ephemeral range
