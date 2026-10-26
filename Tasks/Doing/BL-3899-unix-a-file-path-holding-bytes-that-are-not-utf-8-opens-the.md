---
id: BL-3899
title: Unix: a file path holding bytes that are not UTF-8 opens the raw bytes, as open() does (CGR-114)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs, Curl.Core.UnitTests/FileSystem/PhysicalFileSystemRawByteNameTests.cs, Curl.Protocol.Abstractions.UnitLibrary/RawBytePath.cs, Curl.Protocol.Abstractions.UnitTests/RawBytePathTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3899 — Unix: a file path holding bytes that are not UTF-8 opens the raw bytes, as open() does (CGR-114)

## Goal

On Linux and macOS, a `file://` URL whose `%XX` escapes decode to bytes that are not valid UTF-8 opens the file whose name is exactly those bytes, as curl's `open()` does, so CGR-114's Linux repro matches real curl.

## Context

CGR-114 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (filed from BL-3643). Upstream passes the decoded bytes to `open()` unchanged: with a file named by the raw bytes `caf�.txt`, `curl -sS file://$PWD/caf%E9.txt` prints it and exits 0 (curl 8.18.0 OpenSSL, Linux). Curl exits 37, because `FileUrlPath` decodes escapes as UTF-8 into a .NET string, and .NET's file APIs cannot name a non-UTF-8 file.
This task provides the hook only: a way for `Curl.Protocol.File.UnitLibrary` (which references only `Curl.Protocol.Abstractions.UnitLibrary`) to hand `IFileSystem` a path holding arbitrary bytes, for example a byte-keeping text form in `Curl.Protocol.Abstractions.UnitLibrary/RawBytePath.cs` (the idea of `Curl.Authentication.UnitLibrary/InvalidUtf8ByteEscapingEncoding.cs`, CGR-053: each byte outside valid UTF-8 kept as a lone surrogate), and `PhysicalFileSystem` opening such a path on Unix through the raw bytes (`open(2)` via `LibraryImport`, AOT-safe, then a `FileStream` on the handle). Using it from `FileUrlPath` is BL-3643's work. Windows is unchanged.

## Acceptance criteria

- [ ] `PhysicalFileSystem` on Linux and macOS opens, for reading, a file whose name holds a byte that is not valid UTF-8 when given the raw-byte path form; a path in that form that names no file still fails as a missing file does today.
- [ ] Unit tests in `Curl.Protocol.Abstractions.UnitTests` cover the raw-byte path form, and tests in `Curl.Core.UnitTests` (Unix-only where they create such a file, with `OSCondition`) cover the open.
- [ ] Coverage and complexity gates hold for every library changed (`Measure-CodeQuality.ps1`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: fb155554 f361d830 002d20a6 779790c0.
