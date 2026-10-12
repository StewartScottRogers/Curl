---
id: BL-2093
title: Fix CI failure test303 on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2093 — Fix CI failure test303 on Linux

## Goal

`test303` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test303` failed on Linux in CI run 38178740830 (https://github.com/StewartScottRogers/Curl/actions/runs/38178740830). First failing commit: 0d182629.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38178740830 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test303` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Flaky, not a Linux bug: CI run 38177462122 passed and 38178740830 failed on the same commit 0d1826298. test303 is an HTTPS case with the case's own -m 8 and a 20 s postcmd wait; on a stalled runner curl's 8 s limit expired before the reply reached it, so the output file was empty. Fix: the ratchet's one retry, which covered only the harness's time limits (BL-1859), now covers any failure of a listed case; a real regression fails twice. Chose the general retry over sniffing each case's timeout option because any listed case can hit its own limit on a stalled runner.
- Measured locally: Curl.Conformance.UnitTests 2651 passed, 0 failed; the full fast suite is green.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. A listed upstream case that fails now gets one retry, so test303's -m 8 expiring on a stalled runner no longer fails CI.
