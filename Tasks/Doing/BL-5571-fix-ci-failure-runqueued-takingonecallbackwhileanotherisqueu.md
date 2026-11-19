---
id: BL-5571
title: Fix CI failure RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-19
completed:
---
# BL-5571 — Fix CI failure RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted on Linux

## Goal

`RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted` failed on Linux in CI run 44618782776 (https://github.com/StewartScottRogers/Curl/actions/runs/44618782776). First failing commit: f00e90e6.

    Assertion failed. Expected condition to be true.

Lanes test only on Windows, so reproduce with `gh run view 44618782776 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunQueued_TakingOneCallbackWhileAnotherIsQueued_KeepsTheArrivedSignalCompleted` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: c195ed96.
