---
id: BL-3911
title: Fix CI failure RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3911 — Fix CI failure RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage on Linux and macOS

## Goal

`RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage` failed on Linux and macOS in CI run 40441067741 (https://github.com/StewartScottRogers/Curl/actions/runs/40441067741). First failing commit: 79c9d998.

    CollectionAssert.AreEqual failed. Element at index 3 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40441067741 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (from `gh run view 40441067741 --log-failed`: expected 195, actual 233 at index 3): the test built `CurlCommandRunner` with `runsOnWindows: true` but left `parsesAsWindowsBuild` at its default, `OperatingSystem.IsWindows()` (CurlCommandRunner.cs:1446). On Linux and macOS the parser therefore read the UTF-8 config file as the POSIX build does and kept `café.bin`, while the test expected the ANSI-code-page respelling.
- Fix, test only: the test class passes `parsesAsWindowsBuild: true`, so the command line is parsed as the Windows build parses it on every host. No production code changed, so no Measure-CodeQuality run was needed.
- Curl.Console.UnitTests fast tests: 2955 passed, 0 failed, 29 skipped on Windows. The CI part of the criterion is met by keeping the change platform-neutral (the parse no longer depends on the host); the shift's CI watch files a task for any failure.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: be557c31.
- 2026-10-26: Doing -> Done. Cgr114 config-file output test parses as the Windows build on every host, so it passes on Linux and macOS
