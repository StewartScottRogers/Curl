---
id: BL-076
title: Document that an IFileSystem read open may be non-seekable for a device
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-009]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-076 — Document that an IFileSystem read open may be non-seekable for a device

## Goal

The `IFileSystem` and `FileOpenResult` documentation stops promising that a read open
is always seekable, because `PhysicalFileSystem` opens a character device or FIFO as a
non-seekable stream of length zero.

## Context

BL-009 found it. `IFileSystem.OpenForReadAsync`'s `<returns>` and
`FileOpenResult`'s `Content` parameter and remarks say a read open is "contractually"
seekable. `PhysicalFileSystem` (Curl.Core.UnitLibrary\FileSystem) returns
`FileStream.CanSeek == false` and `Length == 0` for `NUL` or `/dev/stdin`, as curl's
`fstat` of one reports size 0, and `FileProtocolHandler` now answers an offset on such
a source with exit 36 instead of seeking. BL-009's `touches` did not include the
Abstractions library, so the docs were left for this task.

## Acceptance criteria

- [x] `IFileSystem.OpenForReadAsync` and `FileOpenResult` XML docs say a read open is
      seekable for a regular file and may be non-seekable, with length zero, for a
      character device or FIFO.
- [x] ADR-0002 carries a dated amendment saying the same and that the handler returns
      exit 36 for an offset on a non-seekable source.
- [x] `dotnet build` is clean.

## Notes

- Delivered directly rather than through align-and-document: three doc comments and one ADR amendment, no behaviour change.
- The exit-36 statement is scoped to a download's offset: the upload direction (FileProtocolHandler.TrySkipAsync) skips a non-seekable source by reading and never returns 36, so the docs say "download" to stay true of the code.
- Verified: dotnet build 0 warnings 0 errors; fast tests green; dotnet format --verify-no-changes clean on Curl.Protocol.Abstractions.UnitLibrary.
- Board defect found: four live files carry BL-051 (and two each carry BL-050 and BL-052), because lanes allocated IDs in parallel. task-board.ps1 move -Id BL-051 resolves to a Backlog duplicate, so this task was moved to Done by running the unmodified script against a scratch board holding only this file and copying the result back. Renumbering is forbidden by the board rules, so the collision is left for Stewart.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. IFileSystem and FileOpenResult docs and ADR-0002 say a device or FIFO read open may be non-seekable with length zero, and a download offset on one is exit 36
