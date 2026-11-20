---
id: BL-5579
title: Fix CI failure WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-19
completed:
---
# BL-5579 — Fix CI failure WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout on Windows

## Goal

`WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` failed on Windows in CI run 44645606866 (https://github.com/StewartScottRogers/Curl/actions/runs/44645606866). First failing commit: 3365d21e.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 44645606866 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `WaitAsync_CallbackQueued_ReturnsBeforeTheTimeout` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 2e4719cc.
