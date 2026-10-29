---
id: BL-4043
title: Fix CI failure RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-28
completed:
---
# BL-4043 — Fix CI failure RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte on macOS

## Goal

`RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte` failed on macOS in CI run 40819228354 (https://github.com/StewartScottRogers/Curl/actions/runs/40819228354). First failing commit: cc2a4b57.

    CollectionAssert.AreEqual failed. Element at index 40 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40819228354 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 67bef354.
