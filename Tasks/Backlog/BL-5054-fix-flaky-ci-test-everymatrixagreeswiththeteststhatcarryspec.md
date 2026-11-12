---
id: BL-5054
title: Fix flaky CI test EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement that failed once on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-12
completed:
---
# BL-5054 — Fix flaky CI test EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement that failed once on Linux

## Goal

`EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes on every run on Windows, Linux and macOS; it failed once on Linux in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` failed on Linux in CI run 43320342510 (https://github.com/StewartScottRogers/Curl/actions/runs/43320342510). First failing commit: 0210afd2.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43320342510 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` fail intermittently is named under Notes and removed.
- [ ] `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-12: Created.
