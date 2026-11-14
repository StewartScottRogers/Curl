---
id: BL-5159
title: Fix CI failure RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5159 — Fix CI failure RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly on Linux and macOS and Windows

## Goal

`RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` failed on Linux and macOS and Windows in CI run 43527663292 (https://github.com/StewartScottRogers/Curl/actions/runs/43527663292). First failing commit: 4fd82935.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 43527663292 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The first message wraps at 79 columns at a point set by the temp path length (Linux, macOS). The test now undoes the wrap before asserting. Test-only change, no production code.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: fbe34f51.
- 2026-11-13: Doing -> Done. Test undoes the 79-column wrap before asserting
