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
completed: 2026-10-26
---
# BL-3899 — Unix: a file path holding bytes that are not UTF-8 opens the raw bytes, as open() does (CGR-114)

## Goal

On Linux and macOS, a `file://` URL whose `%XX` escapes decode to bytes that are not valid UTF-8 opens the file whose name is exactly those bytes, as curl's `open()` does, so CGR-114's Linux repro matches real curl.

## Context

CGR-114 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (filed from BL-3643). Upstream passes the decoded bytes to `open()` unchanged: with a file named by the raw bytes `caf�.txt`, `curl -sS file://$PWD/caf%E9.txt` prints it and exits 0 (curl 8.18.0 OpenSSL, Linux). Curl exits 37, because `FileUrlPath` decodes escapes as UTF-8 into a .NET string, and .NET's file APIs cannot name a non-UTF-8 file.
This task provides the hook only: a way for `Curl.Protocol.File.UnitLibrary` (which references only `Curl.Protocol.Abstractions.UnitLibrary`) to hand `IFileSystem` a path holding arbitrary bytes, for example a byte-keeping text form in `Curl.Protocol.Abstractions.UnitLibrary/RawBytePath.cs` (the idea of `Curl.Authentication.UnitLibrary/InvalidUtf8ByteEscapingEncoding.cs`, CGR-053: each byte outside valid UTF-8 kept as a lone surrogate), and `PhysicalFileSystem` opening such a path on Unix through the raw bytes (`open(2)` via `LibraryImport`, AOT-safe, then a `FileStream` on the handle). Using it from `FileUrlPath` is BL-3643's work. Windows is unchanged.

## Acceptance criteria

- [x] `PhysicalFileSystem` on Linux and macOS opens, for reading, a file whose name holds a byte that is not valid UTF-8 when given the raw-byte path form; a path in that form that names no file still fails as a missing file does today.
- [x] Unit tests in `Curl.Protocol.Abstractions.UnitTests` cover the raw-byte path form, and tests in `Curl.Core.UnitTests` (Unix-only where they create such a file, with `OSCondition`) cover the open.
- [x] Coverage and complexity gates hold for every library changed (`Measure-CodeQuality.ps1`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan: `RawBytePath` (Abstractions) keeps each non-UTF-8 byte as U+DC00 + byte, the scheme of `Curl.Cli`'s `InvalidUtf8ByteEscape` (written again because the file protocol references only Abstractions); `PhysicalFileSystem.OpenForReadAsync` off Windows opens a path holding one with `SystemNative_Open` on the raw bytes and wraps the descriptor in a `FileStream`, as `Curl.Console`'s `RawByteFileName` does for `-o`. Decision recorded in ADR-0839.
- Default taken: only the read open takes raw bytes (the criterion names reading); a raw-byte write open or directory listing waits for a case that needs it. errno is mapped as `RawByteFileName` maps it (ENOENT NotFound, EACCES/EPERM AccessDenied, ENOTDIR, EISDIR, else IoError).
- Tests: the routing, errno mapping and FileStream wrapping are covered on every platform through an injected opener (`PhysicalFileSystemRawByteNameTests`); the open of a real raw-byte file and a missing raw-byte name are Unix-only (`OSCondition`), so they were skipped on this Windows lane and only CI runs them. That and the platform-neutral fast tests meet the Linux/macOS side.
- Measure-CodeQuality: Curl.Core.UnitLibrary and Curl.Protocol.Abstractions.UnitLibrary at 100% line and 100% branch, 0 failing (the first run failed the public constructor's platform ternary, now in `RawBytePathReadOpenerFor`). dotnet build is clean; the fast tests pass (43961 passed).

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: fb155554 f361d830 002d20a6 779790c0.
- 2026-10-26: Doing -> Done. PhysicalFileSystem opens a RawBytePath read path under its raw bytes on Linux and macOS (ADR-0839)
