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
completed:
---
# BL-5150 — Fix CI failure RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly on macOS and Windows

## Goal

`RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` failed on macOS and Windows in CI run 43521357508 (https://github.com/StewartScottRogers/Curl/actions/runs/43521357508). First failing commit: 4fd82935.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 43521357508 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_ExpanderOptionInAConfigFile_IsRefusedAsCommandLineOnly` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-13: Created.
