---
id: BL-1092
title: Fix CI failure RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed:
---
# BL-1092 — Fix CI failure RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade on Linux and macOS

## Goal

`RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade` failed on Linux and macOS in CI run 36920828630 (https://github.com/StewartScottRogers/Curl/actions/runs/36920828630). First failing commit: 7a6da1e5.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36920828630 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-01: Created.
