---
id: BL-051
title: Document that an IFileSystem read open may be non-seekable for a device
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-009]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-051 — Document that an IFileSystem read open may be non-seekable for a device

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

- [ ] `IFileSystem.OpenForReadAsync` and `FileOpenResult` XML docs say a read open is
      seekable for a regular file and may be non-seekable, with length zero, for a
      character device or FIFO.
- [ ] ADR-0002 carries a dated amendment saying the same and that the handler returns
      exit 36 for an offset on a non-seekable source.
- [ ] `dotnet build` is clean.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
