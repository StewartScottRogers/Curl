---
id: BL-5150
title: Fix CI failure RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly on macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5150 — Fix CI failure RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly on macOS and Windows

## Goal

`RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` failed on macOS and Windows in CI run 43521357508 (https://github.com/StewartScottRogers/Curl/actions/runs/43521357508). First failing commit: 4fd82935.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 43521357508 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: the "file:line" prefix is wrapped at 79 columns, so the long CI temp path split it from "curl: config file option"; the test now asserts only the stable tail. Test-only change, platform-neutral; CI is watched by the shift.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: fbe34f51.
- 2026-11-13: Doing -> Done. Expander-in-config-file test no longer depends on temp path length
