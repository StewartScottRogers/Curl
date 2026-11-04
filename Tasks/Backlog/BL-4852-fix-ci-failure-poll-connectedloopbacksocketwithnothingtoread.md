---
id: BL-4852
title: Fix CI failure Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4852 — Fix CI failure Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly on Linux

## Goal

`Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly` failed on Linux in CI run 41912866538 (https://github.com/StewartScottRogers/Curl/actions/runs/41912866538). First failing commit: 4749d66a.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41912866538 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-04: Created.
