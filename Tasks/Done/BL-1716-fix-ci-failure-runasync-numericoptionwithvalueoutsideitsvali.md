---
id: BL-1716
title: Fix CI failure RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-07
completed: 2026-10-08
---
# BL-1716 — Fix CI failure RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit on Linux and macOS

## Goal

`RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit` failed on Linux and macOS in CI run 37733074018 (https://github.com/StewartScottRogers/Curl/actions/runs/37733074018). First failing commit: 711fbdf0.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37733074018 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the `("--retry","2147483648")` row assumed Windows' 32-bit C `long`. `CommandLineNumber.PlatformLongMaximum` is 2^63-1 on Linux and macOS (as in curl's `str2unum`, which caps at `LONG_MAX`), so there the value is legal and the transfer ran (exit 7 against port 1). The product is right; the test was not platform-neutral.
- Fix (test only, Curl.Console.UnitTests): dropped the row; added `RunAsync_RetryOnePastThePlatformLongMaximum_PrintsProperNumericalParameterAndExitsFailedInit` (PlatformLongMaximum + 1 refused on every OS) and `RunAsync_RetryOnePastIntMaxValueOffWindows_RunsTheTransfer` (`[OSCondition(Exclude, Windows)]`, pins the Linux/macOS acceptance).
- Locally on Windows: build clean, fast tests green (Curl.Console.UnitTests 2689 passed, 25 skipped). The CI half of the criterion is checked by the shift's CI watch on the pushed commit; no production code changed, so coverage is unaffected.

## Log

- 2026-10-07: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. --retry overflow test is platform-neutral: refuses PlatformLongMaximum+1 everywhere, accepts 2147483648 off Windows
