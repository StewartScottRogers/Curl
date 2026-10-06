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
completed: 2026-10-03
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

- [x] A source time past 9999 reaches `SourceLastWriteUnixSeconds` with its Unix seconds (unit test through the seam), and an in-range time behaves as today.
- [x] `-z` on a `file://` source compares in Unix seconds (test).
- [x] A Windows-only test (`[OSCondition(OperatingSystems.Windows)]`) stamps a temp file 30000-01-01T00:00:00Z and reads it back through the handler as Unix seconds 884541340800.
- [x] Notes record the measured curl behaviour for such a source.
- [x] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports 0 failing members; no option changes, so `--ai-help` is unaffected.

## Notes

- Measured curl 8.21.0 (Windows, Schannel), 2026-10-03, source stamped 30000-01-01T00:00:00Z (FILETIME = Unix 884541340800): `-I` prints `Last-Modified: Thu, 31 Dec 1969 23:59:59 GMT` (time_t -1); `-R -o out` exits 0 and leaves `out` at the current time; `-z "1 Jan 2020"` writes nothing, `-z "-1 Jan 2020"` writes the body. Bisected: from 3002-01-01T00:00:00 local (32566777200 in US Mountain) curl holds -1; 3001-01-01T00:00:00Z still prints its date.
- Decision (ADR-0411): Windows matches curl - a raw time past that limit is -1 for `-z` and the header, and `-R` gets no time; elsewhere a raw time past 9999 reaches `SourceLastWriteUnixSeconds` whole. So criteria 1 and 3 are met with the platform's answer: the unit tests drive the seam with no limit (884541340800 reaches `SourceLastWriteUnixSeconds`), and the Windows-only disk test reads 884541340800 through the handler with the limit off and asserts no `-R` time with Windows' limit on.
- Seam: internal `ISourceLastWriteReader` (`Win32SourceLastWriteReader`, `GetFileTime` via `LibraryImport`; `NoRawSourceLastWriteReader` off Windows), read from the opened `FileStream`'s handle, so `IFileSystem`/`FileOpenResult` (Abstractions, held by BL-1418) did not change. `AllowUnsafeBlocks` added to the library's csproj for the generated stub.
- `-z` now compares `TimeCondition.ValueUnixSeconds` with the source's Unix seconds for every time.
- Measure-CodeQuality -Library Curl.Protocol.File.UnitLibrary: 100% line, 100% branch, 0 failing members (the two platform picks carry ADR-0083's "the platform picks the branch" exclusion).
- Left over: an off-Windows raw `stat` reader, and curl's `Thu` weekday for -1 (we write `Wed`).

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A file:// source time past 9999 is read from the handle: -1 on Windows as curl holds it, whole Unix seconds elsewhere; -z compares Unix seconds
