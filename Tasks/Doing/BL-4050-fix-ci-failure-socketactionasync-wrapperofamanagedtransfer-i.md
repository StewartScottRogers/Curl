---
id: BL-4050
title: Fix CI failure SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-4050 — Fix CI failure SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait on macOS

## Goal

`SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait` failed on macOS in CI run 40828309078 (https://github.com/StewartScottRogers/Curl/actions/runs/40828309078). First failing commit: 3e9937e7.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40828309078 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6c38a7f0.
