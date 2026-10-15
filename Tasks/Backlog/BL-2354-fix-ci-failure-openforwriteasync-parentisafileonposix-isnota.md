---
id: BL-2354
title: Fix CI failure OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests, Curl.Core.UnitLibrary]
requirement: none
created: 2026-10-15
completed:
---
# BL-2354 — Fix CI failure OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory on Linux and macOS

## Goal

`OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory` failed on Linux and macOS in CI run 38808821626 (https://github.com/StewartScottRogers/Curl/actions/runs/38808821626). First failing commit: 1c068661.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 38808821626 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-15: Created.
