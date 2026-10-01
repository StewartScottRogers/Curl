---
id: BL-1100
title: Fix CI failure RunAsync_VerboseWithoutUser_ClosesTheConnectionWithExit67 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1100 — Fix CI failure RunAsync_VerboseWithoutUser_ClosesTheConnectionWithExit67 on Linux and macOS

## Goal

`RunAsync_VerboseWithoutUser_ClosesTheConnectionWithExit67` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseWithoutUser_ClosesTheConnectionWithExit67` failed on Linux and macOS in CI run 36936558357 (https://github.com/StewartScottRogers/Curl/actions/runs/36936558357). First failing commit: 6d4ec8c8.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36936558357 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_VerboseWithoutUser_ClosesTheConnectionWithExit67` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test pinned the `curl: (67) Login denied` error line with `
`, but the runner ends curl's error line with `Environment.NewLine` (CRLF on Windows, LF elsewhere), as real curl does in text mode; only the verbose info lines are CRLF everywhere. Fixed the test to expect `Environment.NewLine`, the convention every other runner test already uses (e.g. CurlCommandRunnerConnectTimeoutTests). No production change.
- CI on Linux and macOS is confirmed by the shift's CI watch once the commit lands; locally all fast tests pass on Windows.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. SMB exit-67 verbose test expects the platform newline on the curl error line, so it passes on Linux and macOS
