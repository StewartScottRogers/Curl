---
id: BL-4042
title: Fix CI failure RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-28
completed:
---
# BL-4042 — Fix CI failure RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter on macOS

## Goal

`RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter` failed on macOS in CI run 40819228354 (https://github.com/StewartScottRogers/Curl/actions/runs/40819228354). First failing commit: cc2a4b57.

    CollectionAssert.AreEqual failed. Element at index 42 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40819228354 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
