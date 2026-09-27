---
id: BL-288
title: Print curl's filetime warning for a -z file lookup that fails off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-246]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: FR-009
created: 2026-09-26
completed: 2026-09-27
---
# BL-288 — Print curl's filetime warning for a -z file lookup that fails off Windows

## Goal

On Linux and macOS, `curl -z <file>` whose lookup fails other than as file not found prints
the `Warning: Failed to get filetime: ...` line the OpenSSL build of curl 8.21.0 prints, before
the two illegal-date lines, and `-z ""`, a directory, and the other Windows cases in ADR-0037
match that build too.

## Context

- BL-246 added the fallback (ADR-0037). `DiskDataFileReader` reports a failure reason only when
  `reportsWindowsErrors` is true; off Windows every failure reads as file not found.
- Upstream `getfiletime` off Windows calls `stat` and, for any `errno` but `ENOENT`, prints
  `Failed to get filetime: <strerror(errno)>`. The exact texts (`Not a directory`,
  `Permission denied`, ...) and whether `-z ""` or a directory warns at all must be measured
  on the OpenSSL build of curl 8.21.0 before they are pinned.
- Also left open by BL-246 on Windows: the DOS devices `con` (`CreateFile failed:
  GetLastError 0x00000057`) and `nul` (`GetFileTime failed: GetLastError 0x00000057`),
  measured on curl 8.21.0 on 2026-09-26.

## Acceptance criteria

- [x] The Linux lines for `-z nodir/x`, `-z <unreadable file>` and `-z ""` are measured on curl
      8.21.0 (OpenSSL build), recorded in `Notes`, and asserted byte for byte in
      `Curl.Cli.UnitTests` through `DiskDataFileReader`'s injected lookup.
- [x] `-z con` and `-z nul` on Windows give curl's measured line, or `Notes` records why they
      cannot through the base class library.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no
      failing member in `Curl.Cli.UnitLibrary`.

## Notes

- **Measured** with curl 8.18.0 (x86_64-pc-linux-gnu, OpenSSL/3.5.5, Ubuntu under WSL), 2026-09-27,
  `curl -z <value> file:///dev/null`, non-root. No 8.21.0 OpenSSL build was at hand; the
  non-Windows branch of `getfiletime` in `src/tool_filetime.c` is byte-identical at the
  `curl-8_18_0` and `curl-8_21_0` tags, so the lines are 8.21.0's too.
  - `-z nodir/x` -> `Warning: Failed to get filetime: No such file or directory`
  - `-z ""` -> `Warning: Failed to get filetime: No such file or directory`
  - `-z missing` -> the same: off Windows curl warns for file not found too (unlike Windows).
  - `-z file/x`, `-z file/` -> `Warning: Failed to get filetime: Not a directory`
  - `-z noaccess/x` (directory mode 000) -> `Warning: Failed to get filetime: Permission denied`
  - `-z <unreadable file>` (mode 000) and `-z <directory>` -> no line at all: `stat` needs no read
    access, so curl uses their time. The two illegal-date lines follow every filetime line.
  - 300-character name -> `File name too long`; symlink loop -> `Too many levels of symbolic
    links`; dangling symlink -> `No such file or directory`.
- **Decision (ADR-0070):** off Windows the lookup stands in for `stat` with `File.GetAttributes` +
  `File.GetLastWriteTimeUtc(path)`, and maps exceptions to `strerror` texts. .NET reports
  `ENOTDIR` as `DirectoryNotFoundException` (probed on Linux), so a file among the ancestors, or a
  trailing separator after a file, is reported as `Not a directory` by the lookup itself.
  `DiskDataFileReader.ForPlatform(bool isWindows)` is public so both lookups are tested on
  Windows, as `CommandLineNumber.LongMaximumFor` does.
- **con / nul:** cannot through the base class library's file API. .NET opens both devices with
  `GENERIC_READ` and then fails in `GetFileInformationByHandleEx` with `ERROR_INVALID_FUNCTION`
  (0x80070001) for each, probed on .NET 10.0.401, so nothing separates curl's `CreateFile failed:
  GetLastError 0x00000057` (con) from `GetFileTime failed: GetLastError 0x00000057` (nul). Filed as
  BL-378 (P/Invoke `CreateFileW` + `GetFileTime`). Symlink loops/dangling links off Windows filed as
  BL-377.
- **Touches:** added `Documentation/Planning/Decisions` for ADR-0070 and the ADR-0037 status line;
  no task in Doing names it.
- **Quality gate:** `CommandLineParser.ParseShortBundle` already measured complexity 12 in
  `Measure-CodeQuality.ps1` before this task; its loop body moved into
  `ApplyBundleLetterEndsBundle` so Curl.Cli.UnitLibrary reports 0 failing members (100% line,
  100% branch, worst CRAP 10). The failing members left in Curl.Console and Curl.Networking are
  outside this task.
- The scratch-directory tests in `DiskDataFileReaderTests` touch the temp folder without the
  `Integration` category, following the `ForProcess` disk tests already in that class; they are
  what covers the `stat` stand-in in the fast run the coverage gate measures.
- `dotnet format --verify-no-changes` reports end-of-line errors in
  `CommandLineAuthAndProxyOptionTests.cs`, a file this task does not touch; the changed files are clean.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -z file lookups off Windows print curl's stat strerror line (No such file or directory, Not a directory, Permission denied) and read directories and unreadable files as stat does
