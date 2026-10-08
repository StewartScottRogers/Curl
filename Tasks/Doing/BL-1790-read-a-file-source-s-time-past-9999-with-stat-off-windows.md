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
completed:
---
# BL-1790 — Read a file:// source's time past 9999 with stat off Windows

## Goal

On Linux and macOS, `file://` reads a source file's modification time past year 9999 as Unix seconds through `stat`, so `-R` and `-z` see the real time as they do on Windows.

## Context

ADR-0410 decision 3. Today `FileProtocolHandler` picks `NoRawSourceLastWriteReader` off Windows (`Curl.Protocol.File.UnitLibrary/SourceLastWriteReaders.cs`), which reads nothing, so only the `DateTime` view (capped at 9999) is used there. A POSIX `st_mtime` is 64-bit. Found while fixing AF-0088 (BL-1766), which corrected the ADR to describe the code as it is. Mind `struct stat` layout differences between Linux (x64/arm64) and macOS; `fstat` on the open handle avoids a second path lookup. Base class library only (`LibraryImport`, AOT-safe).

## Acceptance criteria

- [ ] Off Windows, `FileProtocolHandler` uses a `stat`-based `ISourceLastWriteReader` that returns the source's `st_mtime` seconds, including a time past 9999.
- [ ] Unit tests cover the reader's branches, platform-neutral or OS-conditioned; Curl.Protocol.File.UnitLibrary stays at 100% line and branch coverage.
- [ ] ADR-0410 decision 3 states the off-Windows read as code, not intent.
- [ ] `dotnet build` and the fast tests pass.

## Notes


## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
