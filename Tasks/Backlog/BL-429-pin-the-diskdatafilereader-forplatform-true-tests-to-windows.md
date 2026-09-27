---
id: BL-429
title: Pin the DiskDataFileReader ForPlatform(true) tests to Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-424]
touches: [Curl.Cli.UnitTests/DiskDataFileReaderTests.cs]
requirement: none
created: 2026-09-27
completed:
---
# BL-429 — Pin the DiskDataFileReader ForPlatform(true) tests to Windows

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

- [ ] Each of the four test methods listed above in
  `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs` carries
  `[OSCondition(OperatingSystems.Windows)]`, or has been rewritten so it passes on Linux.
- [ ] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green on Windows.
- [ ] The `CI` workflow run for the pushed commit reports `Curl.Cli.UnitTests` with
  Failed: 0 on `ubuntu-latest` and `macos-latest`.
- [ ] No file other than `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs` (and this task
  file) is changed.

## Notes

## Log

- 2026-09-27: Created.
