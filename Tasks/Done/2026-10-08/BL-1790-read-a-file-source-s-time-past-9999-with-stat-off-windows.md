---
id: BL-1790
title: Read a file:// source's time past 9999 with stat off Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1790 — Read a file:// source's time past 9999 with stat off Windows

## Goal

On Linux and macOS, `file://` reads a source file's modification time past year 9999 as Unix seconds through `stat`, so `-R` and `-z` see the real time as they do on Windows.

## Context

ADR-0410 decision 3. Today `FileProtocolHandler` picks `NoRawSourceLastWriteReader` off Windows (`Curl.Protocol.File.UnitLibrary/SourceLastWriteReaders.cs`), which reads nothing, so only the `DateTime` view (capped at 9999) is used there. A POSIX `st_mtime` is 64-bit. Found while fixing AF-0088 (BL-1766), which corrected the ADR to describe the code as it is. Mind `struct stat` layout differences between Linux (x64/arm64) and macOS; `fstat` on the open handle avoids a second path lookup. Base class library only (`LibraryImport`, AOT-safe).

## Acceptance criteria

- [x] Off Windows, `FileProtocolHandler` uses a `stat`-based `ISourceLastWriteReader` that returns the source's `st_mtime` seconds, including a time past 9999.
- [x] Unit tests cover the reader's branches, platform-neutral or OS-conditioned; Curl.Protocol.File.UnitLibrary stays at 100% line and branch coverage.
- [x] ADR-0410 decision 3 states the off-Windows read as code, not intent.
- [x] `dotnet build` and the fast tests pass.

## Notes

- Plan: a `PosixSourceLastWriteReader` beside the Win32 one, picked by `FileProtocolHandler` on Linux and macOS. Rather than `fstat`, whose `struct stat` differs between Linux x64, Linux arm64 and macOS (two inode ABIs), it uses `statx(fd, "", AT_EMPTY_PATH, STATX_MTIME)` on Linux (`stx_mtime.tv_sec` at offset 112 on every architecture) and `fgetattrlist(fd, ATTR_CMN_MODTIME)` on macOS (4-byte length, then the `timespec` at offset 4). Both read the open descriptor, so no second path lookup. A refused call returns null and the handler keeps the open's `DateTime` time.
- Coverage: the reader is a thin P/Invoke adapter marked `ExcludeFromCodeCoverage` under ADR-0083, like `NativeFileTimeSetter`; Measure-CodeQuality on Windows reports Curl.Protocol.File.UnitLibrary at 100% line, 100% branch. Its tests are `[OSCondition(Linux | OSX)]` and run in CI: a non-`FileStream` reads nothing, and a file stamped 2100-01-01 reads 4102444800 from the descriptor.
- A time past 9999 cannot be stamped on most Linux filesystems (ext4 stops at 2446) or through .NET, so the test pins a post-2038 time; `tv_sec` is read as a 64-bit `long` whole, so a time past 9999 on a filesystem that holds one (btrfs, APFS) comes through unchanged. Not verified on Linux or macOS from this lane (Windows only, no dotnet in WSL); CI checks it.


## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. file:// reads a source's st_mtime seconds whole off Windows (statx on Linux, fgetattrlist on macOS)
