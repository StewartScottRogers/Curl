---
id: BL-1093
title: Fix CI failure RunAsync_VerboseRetrWithStls_WritesTheSessionLinesAroundTheUpgrade on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1093 — Fix CI failure RunAsync_VerboseRetrWithStls_WritesTheSessionLinesAroundTheUpgrade on Linux and macOS

## Goal

`RunAsync_VerboseRetrWithStls_WritesTheSessionLinesAroundTheUpgrade` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseRetrWithStls_WritesTheSessionLinesAroundTheUpgrade` failed on Linux and macOS in CI run 36920828630 (https://github.com/StewartScottRogers/Curl/actions/runs/36920828630). First failing commit: 7a6da1e5.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36920828630 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_VerboseRetrWithStls_WritesTheSessionLinesAroundTheUpgrade` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Already fixed by BL-1090 (commit 53190660): the test was split into `RunAsync_VerboseRetrWithStlsOnWindows_WritesTheSchannelLinesAroundTheUpgrade` (Windows only, Schannel lines) and `RunAsync_VerboseRetrWithStlsOffWindows_WritesTheOpenSslTrustLineAroundTheUpgrade` (Linux and macOS, OpenSSL "SSL Trust" line). The failing name no longer exists. CI run 36922316653 (head c8beac60, which contains 53190660) passed on Windows, Linux and macOS. Local build clean, all fast tests green (Curl.Console.UnitTests 2017 passed). No code change needed.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Already fixed by BL-1090: the STLS verbose test is pinned per platform and CI is green on all three
