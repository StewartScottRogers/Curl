---
id: BL-752
title: Match curl's --remove-on-error warning for a non-regular output file off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-494]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-752 — Match curl's --remove-on-error warning for a non-regular output file off Windows

## Goal

A failed `--remove-on-error` transfer whose output file is not a regular file (`-o /dev/null` on Linux and macOS) prints what the OpenSSL curl 8.21.0 prints there, byte for byte.

## Context

- BL-494 made `--remove-on-error` delete a failed transfer's output file through `IOutputPaths.TryDeleteFile` (`Curl.Console/PhysicalOutputPaths.cs`), and print `Warning: Failed removing: <file>` whenever that returns false, as the Windows curl 8.21.0 does for `-o NUL` (measured, BL-494 Notes).
- curl's `post_per_transfer` stats the file first and, for something that is not a regular file, is believed to print a different warning (`Skipping removal; not a regular file: <file>`) instead of trying to unlink it. That was not measured: the lane had only the Windows curl.
- Start at `CurlCommandRunner.RemoveOutputFileOfFailedTransferAsync`.

## Acceptance criteria

- [x] Measured on a Linux or macOS curl 8.21.0: `curl --remove-on-error -o /dev/null <URL cut short>` stderr and exit code copied into Notes.
- [x] `IOutputPaths` tells a non-regular file apart from a failed delete, and `Curl.Console.UnitTests` pins the measured line in a test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` or through the fake, platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01 in Docker, `curlimages/curl:8.21.0` (curl 8.21.0, x86_64-pc-linux-musl,
  OpenSSL/3.5.7), against busybox `nc` answering `HTTP/1.1 200 OK`, `Content-Length: 100` and 3
  body bytes:
  - `curl --no-progress-meter --remove-on-error -o /dev/null http://127.0.0.1:8080/` - exit 18,
    stderr `curl: (18) end of response with 97 bytes missing\nWarning: Skipping removal; not a regular file: /dev/null\n`.
  - The same with `-s` - exit 18, stderr empty.
  - A failed connect (`http://127.0.0.1:1/`) - exit 7, no removal line (the file was never opened).
- curl 8.21.0's `src/tool_operate.c` confirms it: `if(!curlx_stat(...) && S_ISREG(...))` unlink,
  else `warnf("Skipping removal; not a regular file: %s")` - so off Windows a vanished file or a
  directory gets the same line.
- Decision (ADR-0332): `IOutputPaths.TryDeleteFile` became `RemoveFile`, returning
  `OutputFileRemoval` (`Removed`, `Failed`, `NotRegularFile`). .NET has no public API telling
  `/dev/null` from a regular file (probed `File.GetAttributes`, `CanSeek`, `RandomAccess.GetLength`
  in `mcr.microsoft.com/dotnet/sdk:10.0`), so `NativeRegularFileTest` calls the runtime's
  `SystemNative_Stat` shim (one record layout on Linux and macOS; probed: `/dev/null` mode
  `0x21b6`, a file `0x81a4`). Windows passes no test and keeps BL-494's measured `Failed removing: NUL`.
- The measured line is pinned through the fake, platform-neutral
  (`RunAsync_RemoveOnErrorOutputNotARegularFile_WarnsSkippingRemoval`), and the real `stat` by
  `NativeRegularFileTestTests` and `RemoveFile_DevNullOffWindows_IsNotRegularFile`, which run on
  Linux and macOS CI only.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A failed --remove-on-error transfer to a non-regular file off Windows prints curl's 'Skipping removal; not a regular file' warning and leaves it alone
