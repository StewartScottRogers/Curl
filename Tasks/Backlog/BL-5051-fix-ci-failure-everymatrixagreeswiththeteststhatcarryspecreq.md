---
id: BL-5051
title: Fix CI failure EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-12
completed:
---
# BL-5051 — Fix CI failure EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement on Linux and Windows

## Goal

`EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` failed on Linux and Windows in CI run 43305315824 (https://github.com/StewartScottRogers/Curl/actions/runs/43305315824). First failing commit: c7870da8.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43305315824 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-12: Created.
