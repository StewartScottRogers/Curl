---
id: BL-026
title: Oblige IFileSystem implementations never to throw on an open failure
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-008]
requirement: none
created: 2026-09-25
completed:
---
# BL-026 — Oblige `IFileSystem` implementations never to throw on an open failure

## Goal

The `IFileSystem` contract is documented to require every open failure to come back as a
`FileAccessStatus`, never as an exception, so `PhysicalFileSystem` (BL-009) inherits the
exit 37 and exit 23 requirement explicitly instead of by inference.

## Context

`Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs` promises in its class remarks that
"a transfer failure is returned as a `TransferResult` and never thrown; only cancellation
leaves this handler as an exception". That promise holds only if
`IFileSystem.OpenForReadAsync` and `IFileSystem.OpenForWriteAsync` map every reason an open
can fail onto a `FileAccessStatus` and return `FileOpenResult.Failed`. An implementation
that lets an exception escape turns a curl-compatible exit 37 or 23 into an unhandled
crash.

That obligation is real and load-bearing, and nothing states it.
`Curl.Protocol.Abstractions.UnitLibrary\IFileSystem.cs` documents what the members do and
why there is no `Exists` or `Stat`, but never says an implementation must not throw. It
matters because `FileUrlPath` deliberately forwards paths the operating system may reject:
`c|/Windows` (the bar is preserved, not rewritten to a colon), a literal `%` from a
malformed escape, and — outside `--path-as-is` — whatever a dot-segment resolution leaves
behind. Those reach `System.IO` as `ArgumentException`, `NotSupportedException`,
`PathTooLongException`, `DirectoryNotFoundException`, `UnauthorizedAccessException` or
`IOException`, and all of them are exit 37 for a read open and exit 23 for a write open
(<https://curl.se/libcurl/c/libcurl-errors.html>, checked against curl 8.21.0).

`PhysicalFileSystem` does not exist yet — BL-009 — which makes this the right moment to
write the obligation down rather than discover it in a stack trace.

## Acceptance criteria

- [ ] `Curl.Protocol.Abstractions.UnitLibrary\CLAUDE.md` gains a section stating that an
      `IFileSystem` implementation returns `FileOpenResult.Failed` with a
      `FileAccessStatus` for every reason an open can fail, and lets no exception escape
      either open member except an `OperationCanceledException` from the
      `CancellationToken`.
- [ ] The same section names the exception types a `System.IO`-based implementation has to
      absorb — at least `ArgumentException`, `NotSupportedException`,
      `PathTooLongException`, `DirectoryNotFoundException`, `FileNotFoundException`,
      `UnauthorizedAccessException` and `IOException` — and states that the path may be
      syntactically invalid for the platform because `FileUrlPath` forwards it as curl
      does, giving `c|/Windows` and a literal `%` as examples.
- [ ] The same section states the consequence in curl's terms: a failed read open is exit
      37 (`CURLE_FILE_COULDNT_READ_FILE`) and a failed write open is exit 23
      (`CURLE_WRITE_ERROR`), whichever operating-system error occurred, citing
      <https://curl.se/libcurl/c/libcurl-errors.html> and curl 8.21.0.
- [ ] `Documentation\Planning\Decisions\ADR-0002-ifilesystem-as-the-second-protocol-seam.md`
      records the obligation under its consequences, dated, so the seam's decision record
      carries it too.
- [ ] No `.cs` file is changed by this task.

## Notes

The XML documentation on `IFileSystem.cs` is where a C# caller would most naturally read
this, but that is a `.cs` file and `docs-writer` does not edit those. Whoever next changes
`IFileSystem.cs` under a `feature` pipeline — BL-018 is the first — should copy the
obligation into the interface's remarks.

BL-009 now depends on this task and has an acceptance criterion requiring
`PhysicalFileSystem` to honour it.

## Log

- 2026-09-25: Created.
