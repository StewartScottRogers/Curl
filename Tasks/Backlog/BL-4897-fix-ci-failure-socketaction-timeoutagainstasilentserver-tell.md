---
id: BL-4897
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-4897 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on Linux in CI run 42239087175 (https://github.com/StewartScottRogers/Curl/actions/runs/42239087175). First failing commit: e9ba9daf.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=1 T400+ S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 42239087175 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-06: Created.
