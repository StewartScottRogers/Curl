---
id: BL-1433
title: Word -R and --create-dirs file-system failures off Windows as curl's POSIX build does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1432]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-011
created: 2026-10-04
completed:
---
# BL-1433 — Word -R and --create-dirs file-system failures off Windows as curl's POSIX build does

## Goal

Off Windows, Curl words a failed `-R` time stamp and a `--create-dirs` directory it cannot make as curl 8.21.0's POSIX (OpenSSL) build does: `Warning: Failed to set filetime <seconds> on '<outfile>': <strerror text>` and, per `errno`, curl's own `--create-dirs` messages, instead of the Windows-only texts it prints on every platform today.

## Context

- `-R` (upstream `src/tool_filetime.c` lines 128-148, tag `curl-8_21_0`): on a `utimes` (or `utime`) failure curl writes `warnf("Failed to set filetime %" CURL_FORMAT_CURL_OFF_T " on '%s': %s", filetime, filename, curlx_strerror(errno))`, i.e. `Warning: Failed to set filetime 1700000000 on 'out.txt': No such file or directory`. On Windows (lines 107-126) it is `... on outfile: CreateFile failed: GetLastError 0x%08lx` when the open fails and `... on outfile: SetFileTime failed: GetLastError 0x%08lx` when the stamp fails.
- Curl today: `Curl.Console/RemoteTimeFailureWarning.cs` `For` always returns the Windows `CreateFile` form (its remarks say the POSIX form was not measured). `Curl.Core.UnitLibrary/FileSystem/NativeFileTimeSetter.cs` already returns `errno` from `utimes` off Windows, so `CurlCommandRunner.StampOutputFileTimeAsync` has what it needs; it must pass the `-o` name as given.
- `--create-dirs` (upstream `src/tool_dirhie.c` lines 56-91 and 140-146): a `mkdir` failure other than `EACCES` or `EEXIST` (both ignored, so traversal of an existing or unreadable parent continues) prints `curl: The directory name <dir> is too long` for `ENAMETOOLONG`, `curl: <dir> resides on a read-only file system` for `EROFS`, `curl: No space left on the file system that would contain the directory <dir>` for `ENOSPC`, `curl: Cannot create directory <dir> because you exceeded your quota` for `EDQUOT`, and `curl: Error creating directory <dir>` for anything else, then exit 23. Curl today: `CurlCommandRunner.ReportCannotCreateDirectoryAsync` always prints `Error creating directory`, and `PhysicalOutputPaths.TryCreateDirectory` returns only a bool. The Windows build's `_wmkdir` maps Win32 errors to the same errno values; keep Windows's measured output unchanged unless you measure it differently.
- The strerror texts are the C library's: glibc and macOS agree on `No such file or directory` (ENOENT), `Permission denied` (EACCES), `Operation not permitted` (EPERM), `Not a directory` (ENOTDIR), `Read-only file system` (EROFS); the errno numbers of `ENAMETOOLONG` and `EDQUOT` differ between Linux and macOS, so map by platform. .NET on Unix raises `IOException` with `HResult` set to the raw errno for unmapped errors, `UnauthorizedAccessException` for EACCES/EPERM and `PathTooLongException` for ENAMETOOLONG; read the errno from those. BCL only.
- Depends on BL-1432 only because both change `Curl.Console`.

## Acceptance criteria

- [ ] `RemoteTimeFailureWarning` gives the POSIX form off Windows, taking the output file name and errno, with a data-driven test over ENOENT, EACCES, EPERM, EROFS and an unknown errno (glibc `Unknown error <n>`, macOS `Unknown error: <n>`), each platform's text pinned through an injected platform flag so every row runs on every OS.
- [ ] A test pins the Windows `SetFileTime failed` form for a stamp that fails after the file opened, and the existing `CreateFile failed` tests still pass.
- [ ] `--create-dirs` tests in `Curl.Console.UnitTests` pin each of the five messages above by injected errno, and that EACCES and EEXIST on a leading directory are skipped so the next directory is tried, as in `create_dir_hierarchy`.
- [ ] Test paths are drive-less; `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
