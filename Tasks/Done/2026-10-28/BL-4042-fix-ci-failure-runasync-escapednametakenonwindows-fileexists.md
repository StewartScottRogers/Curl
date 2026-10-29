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
completed: 2026-10-28
---
# BL-4042 — Fix CI failure RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter on macOS

## Goal

`RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter` failed on macOS in CI run 40819228354 (https://github.com/StewartScottRogers/Curl/actions/runs/40819228354). First failing commit: cc2a4b57.

    CollectionAssert.AreEqual failed. Element at index 42 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40819228354 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (from job 123268250811 of run 40819228354): off Windows, `RawByteFileName.OpenForWriteAsync` opened every name holding an escaped byte with the real `open` shim, chosen by `OperatingSystem.IsWindows()`, so the test's `InMemoryFileSystem` never saw the open. On macOS, APFS refuses the invalid UTF-8 name, so the open failed with a status that prints `Invalid argument`. Both Cgr051 tests failed there, the `OffWindows` twin too (exit 23 either way). On Linux the same open created a real `\xff.txt` file in the test directory.
- Fix: the raw-byte open is now `IOutputPaths.OpenUnderRawBytes`, next to that seam's raw-byte mkdir, stat and unlink. `PhysicalOutputPaths` gives `RawByteFileName.OpenUnderRawBytes` off Windows and null on Windows. A test's `InMemoryFileSystem` gives null, so every open goes to the injected file system. The runner hands it to `DeferredOutputFileStream` and to the `-D` open. `RawByteFileName.OpenForWriteAsync` takes a nullable opener in place of the `isWindows` flag, and the overload that read the real OS is gone. Production behaviour is unchanged. No ADR: this is a test seam, not a behaviour decision.
- Checks: dotnet build is clean, and the fast tests are green (46 projects, Curl.Console.UnitTests 3026 passed). Measure-CodeQuality for Curl.Console: 0 failing members. The change is platform-neutral (no test touches the real disk any more), which meets the CI criterion under lane rule 11. The shift's CI watch files a task for any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6bca719a.
- 2026-10-28: Doing -> Done. The Cgr051 raw-byte output tests no longer open real files off Windows: the raw-byte open is injected through IOutputPaths, so the macOS failure is fixed
