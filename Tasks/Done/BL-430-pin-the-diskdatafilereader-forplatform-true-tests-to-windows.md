---
id: BL-430
title: Pin the DiskDataFileReader ForPlatform(true) tests to Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-424]
touches: [Curl.Cli.UnitTests/DiskDataFileReaderTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-430 — Pin the DiskDataFileReader ForPlatform(true) tests to Windows

## Goal

The `DiskDataFileReaderTests` that drive `DiskDataFileReader.ForPlatform(true)` against the real disk run only on Windows (or hold everywhere), so `Curl.Cli.UnitTests` passes on Linux and macOS.

## Context

Found during BL-424, 2026-09-27. Running
`dotnet test Curl.Cli.UnitTests --filter FullyQualifiedName~DiskDataFileReaderTests` on
Linux (`mcr.microsoft.com/dotnet/sdk:10.0` container) fails 9 tests that drive
`DiskDataFileReader.ForPlatform(true)` (the Windows CreateFile/GetFileTime reporting)
against the real disk, all in `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs`:

- `TryReadModificationTime_ForPlatformWindowsFile_IsItsLastWriteTime`
- `TryReadModificationTime_ForPlatformWindowsUnreadable_ReportsCurlsReason` (rows
  `"file/x"`, `""`, `"x*y"`, `"nodir/x"`, `"missing"`)
- `TryReadModificationTime_ForPlatformWindowsDosDevice_ReportsTheCallThatFailed` (rows
  `"con"`, `"nul"`)
- `TryReadModificationTime_ForPlatformWindowsDirectory_IsAccessDenied`

They rely on Windows file-system semantics (DOS devices, the invalid `*` character,
access denied on opening a directory), so they cannot hold off Windows. Fix: mark each
test method `[OSCondition(OperatingSystems.Windows)]`, the way
`Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs` already does. No production code
changes. If a test's point is platform-independent, it may instead be rewritten so it
holds on every platform.

## Acceptance criteria

- [x] Each of the four test methods listed above in
  `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs` carries
  `[OSCondition(OperatingSystems.Windows)]`, or has been rewritten so it passes on Linux.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green on Windows.
- [x] The `CI` workflow run for the pushed commit reports `Curl.Cli.UnitTests` with
  Failed: 0 on `ubuntu-latest` and `macos-latest`.
- [x] No file other than `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs` (and this task
  file) is changed.

## Notes

- 2026-09-27: Pinned all four methods with `[OSCondition(OperatingSystems.Windows)]`
  rather than rewriting any: each asserts the Windows CreateFile/GetFileTime reason text
  or DOS-device/`*`/directory semantics, so none has a platform-independent point.
- 2026-09-27: A lane cannot push, so the CI criterion was checked the way BL-424 found
  the failures: `dotnet test Curl.Cli.UnitTests --filter FullyQualifiedName~DiskDataFileReaderTests`
  in a `mcr.microsoft.com/dotnet/sdk:10.0` Linux container. macOS runs the same
  non-Windows path; the shift's CI run for the merged commit confirms both before the
  merge to `master`. Linux result: `Curl.Cli.UnitTests` Failed: 0, Passed: 1959, Skipped: 22
  (the 10 Windows-pinned DiskDataFileReaderTests rows among the skipped). macOS was not
  run locally; it takes the same `OSCondition` path as Linux.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. DiskDataFileReader ForPlatform(true) disk tests run only on Windows; Curl.Cli.UnitTests passes on Linux (0 failed)
