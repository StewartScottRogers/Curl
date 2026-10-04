---
id: BL-1418
title: Report a directory's last-write time from PhysicalFileSystem's IsDirectory open
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1401]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: FR-004
created: 2026-10-03
completed:
---
# BL-1418 — Report a directory's last-write time from PhysicalFileSystem's IsDirectory open

## Goal

Off Windows, `PhysicalFileSystem.OpenForReadAsync` on a directory returns `FileAccessStatus.IsDirectory` with `LastWriteTimeUtc` set to the directory's modification time, so `curl -i file:///dir/` writes `Last-Modified` and `-z` compares against it, as curl 8.21.0's Linux and macOS builds do.

## Context

- BL-1401 made `FileProtocolHandler` list a directory off Windows; it writes `Last-Modified` and applies `-z` from `FileOpenResult.LastWriteTimeUtc` of the failed open (`FileOpenResult.Failed(FileAccessStatus.IsDirectory) with { LastWriteTimeUtc = ... }`).
- `Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs` returns `FileOpenResult.Failed(...)` with no timestamp, so on a real disk the `-i` block of a listing is only the blank line and `-z` always transfers.
- curl 8.21.0 `lib/file.c` lines 414-420: `fstat` of the directory records its modification time.

## Acceptance criteria

- [ ] A test in `Curl.Core.UnitTests` (Integration, off Windows) opens a temporary directory and asserts `Status == IsDirectory` and `LastWriteTimeUtc == Directory.GetLastWriteTimeUtc(path)`.
- [ ] Windows keeps returning `IsDirectory` (with or without a timestamp; the handler does not list there).
- [ ] `dotnet build` clean; fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member in the changed code.

## Notes

## Log

- 2026-10-03: Created.
