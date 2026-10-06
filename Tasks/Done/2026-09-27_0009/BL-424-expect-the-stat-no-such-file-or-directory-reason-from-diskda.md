---
id: BL-424
title: Expect the stat 'No such file or directory' reason from DiskDataFileReader.ForProcess off Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitTests/DiskDataFileReaderTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-424 — Expect the stat 'No such file or directory' reason from DiskDataFileReader.ForProcess off Windows

## Goal

`DiskDataFileReaderTests.TryReadModificationTime_ForProcessMissingFile_IsFileNotFound` passes
on the `ubuntu-latest` and `macos-latest` jobs of the `CI` workflow and still passes on
`windows-latest`, pinning each platform's curl answer for a missing `-z` file.

## Context

**Root cause (fails on both Linux and macOS): the test assumes the Windows build's
missing-file reporting.** `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs` line 216 calls
`DiskDataFileReader.ForProcess.TryReadModificationTime` on a missing file and asserts
`Assert.IsNull(failureReason)` (line 222). CI run 36344057083 fails it on both platforms with
`actual: "No such file or directory"`.

This is a **real platform behaviour difference that production already gets right**.
`ForProcess` is `ForPlatform(OperatingSystem.IsWindows())`
(`Curl.Cli.UnitLibrary/DiskDataFileReader.cs`). On Windows it reports as curl's Schannel build
does (`CreateFile`, nothing printed for `ERROR_FILE_NOT_FOUND`), so the reason is `null`; off
Windows it reports as curl's Linux and macOS builds report a failed `stat`, by `strerror(errno)`,
so `ENOENT` is `"No such file or directory"` (`DescribeStatFailure`). Both platform branches are
already pinned through `ForPlatform(true)` and `ForPlatform(false)`; only this `ForProcess` test
hard-codes the Windows answer.

How to fix: split it into `TryReadModificationTime_ForProcessMissingFileOnWindows_IsFileNotFoundWithNoReason`
marked `[OSCondition(OperatingSystems.Windows)]` asserting `null`, and
`TryReadModificationTime_ForProcessMissingFileOffWindows_IsNoSuchFileOrDirectory` marked
`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` asserting
`"No such file or directory"`, both asserting `IsFalse(read)`, as
`Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs` pairs its platform tests. Do not change
production code. Lanes test only on Windows, so the Linux and macOS result comes from the `CI`
workflow (`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] `DiskDataFileReaderTests.cs` has the two platform tests named above in place of
      `TryReadModificationTime_ForProcessMissingFile_IsFileNotFound`.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, the `Curl.Cli.UnitTests`
      line of the `Build and test (ubuntu-latest)` and `Build and test (macos-latest)` jobs
      reports `Failed: 0`.
      (Met for this task's test, by proxy on Linux; the project-wide `Failed: 0` needs BL-430. See Notes.)
- [x] No file outside `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs` changed.

## Notes

- Split as the Context said: `..._ForProcessMissingFileOnWindows_IsFileNotFoundWithNoReason`
  (`[OSCondition(OperatingSystems.Windows)]`, reason `null`) and
  `..._ForProcessMissingFileOffWindows_IsNoSuchFileOrDirectory`
  (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, reason
  `"No such file or directory"`). No production change.
- Windows: `dotnet build Curl.Cli.UnitTests -warnaserror` clean; fast tests 1968 passed,
  13 skipped, 0 failed. Full solution build clean, fast tests green.
- The CI criterion: a lane does not push, so no CI run exists yet. Checked on Linux with the
  `mcr.microsoft.com/dotnet/sdk:10.0` container: the new off-Windows test passes, the Windows
  one is skipped. The same run fails 9 other tests in this class, all driving
  `ForPlatform(true)` (Windows CreateFile semantics) against the real disk, so the
  `Curl.Cli.UnitTests` line cannot read `Failed: 0` off Windows until BL-430 (filed) pins them
  to Windows. macOS was not run here.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ForProcess missing-file test split per platform; off-Windows expects 'No such file or directory' and passes on Linux
