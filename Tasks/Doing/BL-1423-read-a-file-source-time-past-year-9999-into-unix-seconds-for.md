---
id: BL-1423
title: Read a file:// source time past year 9999 into Unix seconds for -R and -z
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1427]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1423 — Read a file:// source time past year 9999 into Unix seconds for -R and -z

## Goal

A `file://` source whose last-write time is past 9999-12-31T23:59:59Z gives that time to `-R` as `TransferResult.SourceLastWriteUnixSeconds` and to `-z` in Unix seconds, instead of an unknown time or an exception.

## Context

- ADR-0410, decision 3. `Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs` line ~300 sets `SourceLastWriteTimeUtc` from .NET's `DateTime`-based last-write time, which cannot hold such a time. An NTFS `FILETIME` reaches 30828; a POSIX `stat` `st_mtime` is 64-bit (Linux file systems clamp to their own range, ext4 to 2446).
- Read the raw time where .NET cannot (Win32 `GetFileTime`/`GetFileInformationByHandle` on Windows, `stat` off Windows) through `LibraryImport`, which is AOT-safe; behind the handler's existing file-system seam so tests stay off the disk where they can.
- Measure first: what curl 8.21.0 (Windows) does with `-R -o out file:///<path>` for a source stamped 30000-01-01 (stamp it with `SetFileTime` from PowerShell), and pin that.
- Depends on BL-1427.

## Acceptance criteria

- [ ] A source time past 9999 reaches `SourceLastWriteUnixSeconds` with its Unix seconds (unit test through the seam), and an in-range time behaves as today.
- [ ] `-z` on a `file://` source compares in Unix seconds (test).
- [ ] A Windows-only test (`[OSCondition(OperatingSystems.Windows)]`) stamps a temp file 30000-01-01T00:00:00Z and reads it back through the handler as Unix seconds 884541340800.
- [ ] Notes record the measured curl behaviour for such a source.
- [ ] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports 0 failing members; no option changes, so `--ai-help` is unaffected.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
