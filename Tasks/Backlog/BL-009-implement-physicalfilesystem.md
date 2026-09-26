---
id: BL-009
title: Implement PhysicalFileSystem in Curl.Core.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-006, BL-026]
requirement: none
created: 2026-09-25
completed:
---
# BL-009 — Implement `PhysicalFileSystem` in `Curl.Core.UnitLibrary`

## Goal

A real-disk implementation of the `IFileSystem` contract exists in
`Curl.Core.UnitLibrary`, with its one disk-touching test.

## Context

See `Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md`.
It wraps `System.IO.File` and `FileInfo` behind the `IFileSystem` contract from
`Curl.Protocol.Abstractions.UnitLibrary`, in a `FileSystem\` folder.

## Acceptance criteria

- [ ] A non-seekable source reached through the real `IFileSystem` with `-r` or `-C`
      returns an exit code instead of throwing. `FileProtocolHandler`'s download seek does
      not check `CanSeek` (unlike `TrySkipAsync`, which does), so a character device or
      FIFO such as `file:///dev/stdin` would throw `NotSupportedException` out of
      `ExecuteAsync` - breaking the class's stated contract that a transfer failure is
      returned and never thrown, and that only cancellation escapes as an exception.
      Found by the BL-008 re-review on 2026-09-25; unreachable until this task lands,
      which is why it is here rather than in BL-008.

- [ ] `PhysicalFileSystem` in `Curl.Core.UnitLibrary\FileSystem\` implements
      `IFileSystem` using only `System.IO`.
- [ ] `Curl.Core.UnitTests` has its one disk-touching test, marked
      `[TestCategory("Integration")]`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` excludes that test and
      runs green.
- [ ] `PhysicalFileSystem` lets no exception escape `OpenForReadAsync` or
      `OpenForWriteAsync` except an `OperationCanceledException` from the
      `CancellationToken`: every failure comes back as `FileOpenResult.Failed` with a
      `FileAccessStatus`, per the obligation BL-026 documents in
      `Curl.Protocol.Abstractions.UnitLibrary\CLAUDE.md` and ADR-0002. Tests cover a
      missing file, a directory in place of a file, a path that is invalid for the
      platform (`c|/Windows`, a literal `%`) and a destination directory that does not
      exist, each asserting the `FileAccessStatus` rather than a thrown exception.

## Notes

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-25: Added depends-on BL-026 and the never-throw acceptance criterion, so the
  exit 37 and exit 23 mapping is inherited explicitly rather than by inference.
- 2026-09-25: Added the non-seekable-source criterion; the handler's download seek has no CanSeek check and this task is what makes that reachable.
