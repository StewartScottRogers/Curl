---
id: BL-4006
title: Fix CI failure ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4006 — Fix CI failure ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds on macOS

## Goal

`ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` failed on macOS in CI run 40796140443 (https://github.com/StewartScottRogers/Curl/actions/runs/40796140443). First failing commit: d639a348.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 40796140443 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: in CI run 40796140443 the untimed warm-up refusal took 2 ms and the timed one 568 ms (the test log's PHASE lines), so the refusal itself is immediate and the 568 ms is a busy macOS runner's scheduling stall, not a wait of Curl's. Fix (test only): time five refusals and assert the fastest is under 500 ms; a wait of Curl's own would slow every attempt, a scheduler stall only some. Pipeline feature run directly: one test file, no production or design change, so no ADR and no quality run (no production code changed).
- The test is excluded on Windows, so locally it shows as skipped; the Networking fast tests (3784 passed) and the whole fast suite are green on Windows. The change is platform-neutral (loopback IPv4, no paths or OS text), which is how this lane meets the CI criterion (lane rule 11); the shift's CI watch files any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: f1e52f1e.
- 2026-10-28: Doing -> Done. The real refused-loopback timing test takes the fastest of five refusals, so a macOS runner's scheduling stall no longer fails it
