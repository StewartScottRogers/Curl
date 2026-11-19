---
id: BL-5522
title: --record exits 23 when the record directory denies access
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs, Curl.Extension.RecordReplay.UnitTests/RecordReplayTransferHookTests.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5522 — --record exits 23 when the record directory denies access

## Goal

A `--record` directory that cannot be written because access is denied gives exit 23 "Failed writing the --record directory <dir>", as other I/O failures already do, instead of an unhandled exception.

## Context

Found by the shift-end review of 2026-11-18 (ADR-0710), pr-review-toolkit silent-failure-hunter (rated Important); confirmed against the code.

`Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs:150-159`, `TryRecordAsync`, was added by this shift for STD-ext-095. It catches only `IOException`.

`ExchangeDirectory.WriteAsync` calls three methods that throw `UnauthorizedAccessException` on a read-only or access-denied directory: `Directory.CreateDirectory`, `File.WriteAllBytesAsync` and `File.Move`. `UnauthorizedAccessException` does not derive from `IOException`, so it escapes the hook after the body has already been written.

The sibling hooks catch both exception types, for example `CodeGenerationTransferHook.cs:40` and `CompressionDictionaryTransferHook.cs:134`.

## Acceptance criteria

- [ ] `TryRecordAsync` maps `UnauthorizedAccessException` to the same exit 23 result as `IOException`.
- [ ] A unit test in `RecordReplayTransferHookTests.cs` drives a directory write that throws `UnauthorizedAccessException` and asserts exit 23 with the message naming `--record` and the directory.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 6a0c954f a2495cd0 3d836d5e.
