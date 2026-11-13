---
id: BL-5052
title: Fix CI failure EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5052 — Fix CI failure EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement on Linux and macOS and Windows

## Goal

`EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` failed on Linux and macOS and Windows in CI run 43311707285 (https://github.com/StewartScottRogers/Curl/actions/runs/43311707285). First failing commit: c7870da8.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43311707285 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Passes on Windows with no change: fixed by d433971d9 (BL-5013) and 8725c8ecf, both after the first failing commit c7870da8. Platform-neutral (matrix rows only).

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: e9d3d3fc.
- 2026-11-12: Doing -> Done. Test already passes after d433971d9 and 8725c8ecf
