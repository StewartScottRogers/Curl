---
id: BL-009
title: Implement PhysicalFileSystem in Curl.Core.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-006, BL-026]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-25
completed: 2026-09-26
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

- [x] A non-seekable source reached through the real `IFileSystem` with `-r` or `-C`
      returns an exit code instead of throwing. `FileProtocolHandler`'s download seek does
      not check `CanSeek` (unlike `TrySkipAsync`, which does), so a character device or
      FIFO such as `file:///dev/stdin` would throw `NotSupportedException` out of
      `ExecuteAsync` - breaking the class's stated contract that a transfer failure is
      returned and never thrown, and that only cancellation escapes as an exception.
      Found by the BL-008 re-review on 2026-09-25; unreachable until this task lands,
      which is why it is here rather than in BL-008.

- [x] `PhysicalFileSystem` in `Curl.Core.UnitLibrary\FileSystem\` implements
      `IFileSystem` using only `System.IO`.
- [x] `Curl.Core.UnitTests` has its one disk-touching test, marked
      `[TestCategory("Integration")]`.
- [x] `dotnet test --filter "TestCategory!=Integration"` excludes that test and
      runs green.
- [x] `PhysicalFileSystem` lets no exception escape `OpenForReadAsync` or
      `OpenForWriteAsync` except an `OperationCanceledException` from the
      `CancellationToken`: every failure comes back as `FileOpenResult.Failed` with a
      `FileAccessStatus`, per the obligation BL-026 documents in
      `Curl.Protocol.Abstractions.UnitLibrary\CLAUDE.md` and ADR-0002. Tests cover a
      missing file, a directory in place of a file, a path that is invalid for the
      platform (`c|/Windows`, a literal `%`) and a destination directory that does not
      exist, each asserting the `FileAccessStatus` rather than a thrown exception.

## Notes

- Delivered: `Curl.Core.UnitLibrary\FileSystem\PhysicalFileSystem.cs` (`FileStream`,
  asynchronous; a read shares ReadWrite|Delete like curl's lock-free `open(2)`, a write
  shares Read; Truncate is `FileMode.Create`, Append is `FileMode.Append`) and
  `FileOpenFailure.cs`, the pure exception-to-`FileAccessStatus` mapping. A directory is
  detected with `Directory.Exists` after the failure, because Windows and Linux both
  throw `UnauthorizedAccessException` for one; it wins over the exception type.
- Non-seekable source (criterion 1): `FileProtocolHandler` now returns exit 36 with
  `failed to resume file:// transfer` when the window starts past 0 and the source
  cannot seek, matching curl's exit 36 for a failed `lseek`. `PhysicalFileSystem`
  reports length 0 for a non-seekable handle (curl's `fstat` of a device), so through
  the real file system an offset is usually refused by the length check first; the guard
  covers any implementation that reports a length. Three handler tests pin it.
- Choice: "its one disk-touching test" is read as one Integration test class
  (`PhysicalFileSystemTests`, 11 `[TestCategory("Integration")]` methods), because the
  last criterion asks for four separate disk cases. The two pre-cancelled-token tests
  and all of `FileOpenFailureTests` touch no disk and stay in the fast run.
- Choice: a timestamp the OS will not give for the handle (Windows `NUL`) is `null`,
  per ADR-0002. That drops `Last-Modified` where curl 8.21.0 on Windows prints the
  epoch; filed as BL-052.
- The `IFileSystem`/`FileOpenResult` docs still call a read open always seekable; that
  is outside this task's `touches`, so it is filed as BL-076.
- Coverage: `Curl.Core.UnitLibrary` is 100% line and branch with the Integration tests
  in the run; `PhysicalFileSystem` itself is exercised only there, by design (ADR-0002).
- `Curl.Core.UnitLibrary.csproj` gained `InternalsVisibleTo Curl.Core.UnitTests`, the
  pattern `Curl.Networking.UnitLibrary` already uses.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-25: Added depends-on BL-026 and the never-throw acceptance criterion, so the
  exit 37 and exit 23 mapping is inherited explicitly rather than by inference.
- 2026-09-25: Added the non-seekable-source criterion; the handler's download seek has no CanSeek check and this task is what makes that reachable.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. PhysicalFileSystem opens real files and returns every failed open as a FileAccessStatus; file:// returns exit 36 for an offset on a non-seekable source instead of throwing
