---
id: BL-4027
title: Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-4027 — Fix CI failure SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut on Linux and macOS

## Goal

`SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` failed on Linux and macOS in CI run 40810061535 (https://github.com/StewartScottRogers/Curl/actions/runs/40810061535). First failing commit: 6407f459.

    CollectionAssert.AreEqual failed. T0 S=2 T400+ S=4 T-1. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40810061535 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SocketAction_TimeoutAgainstASilentServer_TellsTheTimeoutThenRemoveWithOperationTimedOut` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
