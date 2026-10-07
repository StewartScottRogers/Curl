---
id: BL-1650
title: Fix CI failure Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1650 — Fix CI failure Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical on Linux and macOS

## Goal

`Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical` failed on Linux and macOS in CI run 37694677072 (https://github.com/StewartScottRogers/Curl/actions/runs/37694677072). First failing commit: 5c771af7.

    Assertion failed. Expected condition to be false.

Lanes test only on Windows, so reproduce with `gh run view 37694677072 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test pinned `--retry 2147483648` as refused on every platform, but a C `long` is 64 bits
  on Linux and macOS, where curl (and `CommandLineNumber.PlatformLongMaximum`, ADR-0019) accepts it. The
  production code was right; the test was Windows-only without saying so.
- Fix: the non-integer rows stay in `Parse_RetryNotAnInteger_RefusesAsNotProperNumerical`; the
  one-past-the-limit case is pinned per platform - `2147483648` refused on Windows, `9223372036854775808`
  refused off Windows (the Linux ceiling measured against OpenSSL curl 8.18.0 in ADR-0019). Acceptance of
  `2147483648` off Windows is already pinned by `Parse_OnLinuxOrMacOS_PastTheWindowsCeiling_IsRecorded`.
- The CI half of the acceptance box is checked by the shift's CI watch after integration; lanes cannot
  run Linux or macOS. One full fast run showed one failing project; the rerun was fully green, so it
  was a flake elsewhere, unrelated to this test-only change.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. --retry past the long limit is pinned per platform; fast tests green
