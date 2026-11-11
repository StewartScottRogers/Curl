---
id: BL-4974
title: Fix CI failure EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4974 — Fix CI failure EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement on Linux and Windows

## Goal

`EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` failed on Linux and Windows in CI run 43074475535 (https://github.com/StewartScottRogers/Curl/actions/runs/43074475535). First failing commit: c5454550.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43074475535 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: e9d3d3fc.
