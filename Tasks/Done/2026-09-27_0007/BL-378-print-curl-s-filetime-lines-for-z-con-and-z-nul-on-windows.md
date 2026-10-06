---
id: BL-378
title: Print curl's filetime lines for -z con and -z nul on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-288]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0088-the-windows-z-file-lookup-calls-createfile-and-getfiletime-as-curl-does.md, Documentation/Planning/Decisions/ADR-0037-a-z-value-that-is-not-a-date-is-read-as-a-file-through-idatafilereader.md]
requirement: FR-009
created: 2026-09-27
completed: 2026-09-27
---
# BL-378 — Print curl's filetime lines for -z con and -z nul on Windows

## Goal

On Windows, `curl -z con` prints `Warning: Failed to get filetime: CreateFile failed: GetLastError 0x00000057` and `curl -z nul` prints `Warning: Failed to get filetime: GetFileTime failed: GetLastError 0x00000057` before the two illegal-date lines, as curl 8.21.0 (Schannel build) does.

## Context

- ADR-0037, ADR-0071 (BL-288): the Windows lookup opens the file with `File.OpenHandle` (`GENERIC_READ`), which succeeds for both devices; `File.GetLastWriteTimeUtc(handle)` then fails with `ERROR_INVALID_FUNCTION` (HResult 0x80070001) for each, so today both print `CreateFile failed: GetLastError 0x00000001`.
- curl opens with `CreateFile(filename, FILE_READ_ATTRIBUTES, share all, OPEN_EXISTING)` then calls `GetFileTime` (`src/tool_filetime.c`, curl-8_21_0 tag). `File.GetAttributes("con")` fails with 0x80070057 on .NET 10, but a directory must still fail as 0x00000005, so it cannot replace the open.
- Likely route: `[LibraryImport]` of kernel32 `CreateFileW` and `GetFileTime` behind the existing `readLastWriteTimeUtc` seam; keep it native-AOT compatible and record the choice in an ADR. No package is needed.

## Acceptance criteria

- [x] `-z con` and `-z nul` give curl's measured lines on Windows, asserted in `Curl.Cli.UnitTests`.
- [x] Every row of ADR-0037's measured table still matches.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Cli.UnitLibrary`.

## Notes

- Plan: `WindowsFileTimeReader` makes curl's own two calls (kernel32 `CreateFileW` with `FILE_READ_ATTRIBUTES`, share all, `OPEN_EXISTING`; then `GetFileTime`) behind the `readLastWriteTimeUtc` seam. A failed call throws `FileTimeLookupException` naming the call and its `GetLastError` code; `DiskDataFileReader` prints that call's name, else `CreateFile`. Recorded in ADR-0088.
- Decision: `[DllImport]`, not `[LibraryImport]`, because the generator needs `AllowUnsafeBlocks` in the project; it follows `Curl.Console`'s `GetStdHandle` import, and native AOT compiles the marshalling ahead of time.
- Touches: added the new ADR-0088 file and ADR-0037 (its status now points at ADR-0088), because the decision rules require an ADR; no task in Doing names `Documentation/Planning/Decisions`.
- Verified: `DiskDataFileReaderTests` runs the real calls for `con`, `nul`, `""`, `nodir/x`, `file/x`, `x*y`, a missing file, a directory and a file; `dotnet run --project Curl.Console -- -z con|nul ...` prints curl's measured lines. `Measure-CodeQuality.ps1`: `Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members (the one failing member each in `Curl.Console` and `Curl.Networking.UnitLibrary` are in BL-416's and BL-369's projects).


## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -z con and -z nul print curl's CreateFile and GetFileTime filetime lines on Windows
