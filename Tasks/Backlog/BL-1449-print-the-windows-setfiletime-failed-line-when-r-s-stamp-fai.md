---
id: BL-1449
title: Print the Windows SetFileTime failed line when -R's stamp fails after the file opened
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1433]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: FR-011
created: 2026-10-04
completed:
---
# BL-1449 — Print the Windows SetFileTime failed line when -R's stamp fails after the file opened

## Goal

On Windows, when `-R` opens the `-o` file but `SetFileTime` refuses the time, Curl prints curl 8.21.0's `Warning: Failed to set filetime <seconds> on outfile: SetFileTime failed: GetLastError 0x<code>` instead of the `CreateFile failed` line.

## Context

- Upstream `src/tool_filetime.c` lines 107-126 (tag `curl-8_21_0`): `CreateFile failed` when the open fails, `SetFileTime failed` when the stamp fails.
- BL-1433 added `RemoteTimeFailureWarning.ForWindowsStamp` (Curl.Console) but the runner always prints `ForWindowsOpen`, because `IFileTimeSetter.TrySetLastWriteUnixSeconds` (Curl.Core, `PhysicalFileSystem`) reports only an error code and `File.SetLastWriteTimeUtc` opens and stamps in one call (ADR-0415, point 4).
- Make the setter say which step failed (for example by opening with `File.OpenHandle` and stamping with `File.SetLastWriteTime(SafeFileHandle, DateTime)` or `NativeFileTimeSetter`), and have `CurlCommandRunner.FileTimeFailureWarning` pick the line.

## Acceptance criteria

- [ ] A `PhysicalFileSystem` test (Windows-only) shows an open failure and a stamp failure reported apart.
- [ ] A `CurlCommandRunnerRemoteTimeTests` test with `runsOnWindows: true` pins the `SetFileTime failed` lines for a stamp failure; the `CreateFile failed` tests still pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Core.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
