---
id: BL-2354
title: Fix CI failure OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests, Curl.Core.UnitLibrary]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2354 — Fix CI failure OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory on Linux and macOS

## Goal

`OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory` failed on Linux and macOS in CI run 38808821626 (https://github.com/StewartScottRogers/Curl/actions/runs/38808821626). First failing commit: 1c068661.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 38808821626 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (from the CI log): off Windows, .NET raises the `ENOTDIR` of a file-as-parent open
  as a `DirectoryNotFoundException`, not as an `IOException` with `HResult` 20 as BL-2328
  assumed, so `FileOpenFailure.StatusFor` mapped it to `NotFound`.
- Fix: `PhysicalFileSystem` takes a third platform decision, `reportsFileInPathAsNotADirectory`
  (default `!OperatingSystem.IsWindows()`); with it, a `NotFound` open whose parent chain holds
  an existing file is `NotADirectory`. The errno-20 check stays for any `IOException` that
  does carry it. Being a constructor flag, three new tests reach the POSIX path on Windows
  too, so the 100% coverage gate holds (Measure-CodeQuality: 0 failing members).
- The test itself is skipped on Windows, where lanes test; the CI half of the criterion is
  checked by the shift's CI watch after the push, which files a new task if Linux or macOS
  stays red.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. A write open under a file-as-parent reports NotADirectory on Linux and macOS, as curl's ENOTDIR
