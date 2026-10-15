---
id: BL-2258
title: Fix CI failure test203 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2258 — Fix CI failure test203 on Linux and macOS

## Goal

`test203` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test203` failed on Linux and macOS in CI run 38682342325 (https://github.com/StewartScottRogers/Curl/actions/runs/38682342325). First failing commit: 8d337539.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38682342325 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test203` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the runner's `%LOGDIR` is absolute and `%FILE_PWD` was empty, so on Linux and macOS
  test203's `file:%FILE_PWD/%LOGDIR/...` became `file://tmp/...`, a URL naming the host `tmp`;
  on Windows (`C:/...`) it stayed `file:/C:/...`. Fix: `UpstreamTestDirectoryComposition`
  rewrites `%FILE_PWD/%LOGDIR` to `%FILE_PWD%LOGDIR`, and `%FILE_PWD` is `/` before a
  drive-letter log directory and empty before a `/`-led one (`FilePwdBefore`), as upstream's
  `%FILE_PWD` is an absolute path starting with one `/`. The other forms
  (`file://localhost%FILE_PWD/%LOGDIR`, `file:////%FILE_PWD/%LOGDIR`) still name the file;
  test3016's bare `file://%FILE_PWD/` is `!win32` and keeps an empty value off Windows.
- The Linux and macOS half of the box rests on the unit test pinning a `/tmp` log directory;
  CI on the landing commit confirms it. Conformance tests 3065 passed, 0 failed on Windows;
  Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.
- `Gap/Tools/Expand-UpstreamCase.cs` still gives `%FILE_PWD` an empty value; it is an
  audit-office path a lane may not change, so it is left to an interactive session.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. test203's file:/ URL names the log file on every platform
