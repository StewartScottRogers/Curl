---
id: BL-378
title: Print curl's filetime lines for -z con and -z nul on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-288]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-009
created: 2026-09-27
completed:
---
# BL-378 — Print curl's filetime lines for -z con and -z nul on Windows

## Goal

On Windows, `curl -z con` prints `Warning: Failed to get filetime: CreateFile failed: GetLastError 0x00000057` and `curl -z nul` prints `Warning: Failed to get filetime: GetFileTime failed: GetLastError 0x00000057` before the two illegal-date lines, as curl 8.21.0 (Schannel build) does.

## Context

- ADR-0037, ADR-0071 (BL-288): the Windows lookup opens the file with `File.OpenHandle` (`GENERIC_READ`), which succeeds for both devices; `File.GetLastWriteTimeUtc(handle)` then fails with `ERROR_INVALID_FUNCTION` (HResult 0x80070001) for each, so today both print `CreateFile failed: GetLastError 0x00000001`.
- curl opens with `CreateFile(filename, FILE_READ_ATTRIBUTES, share all, OPEN_EXISTING)` then calls `GetFileTime` (`src/tool_filetime.c`, curl-8_21_0 tag). `File.GetAttributes("con")` fails with 0x80070057 on .NET 10, but a directory must still fail as 0x00000005, so it cannot replace the open.
- Likely route: `[LibraryImport]` of kernel32 `CreateFileW` and `GetFileTime` behind the existing `readLastWriteTimeUtc` seam; keep it native-AOT compatible and record the choice in an ADR. No package is needed.

## Acceptance criteria

- [ ] `-z con` and `-z nul` give curl's measured lines on Windows, asserted in `Curl.Cli.UnitTests`.
- [ ] Every row of ADR-0037's measured table still matches.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
