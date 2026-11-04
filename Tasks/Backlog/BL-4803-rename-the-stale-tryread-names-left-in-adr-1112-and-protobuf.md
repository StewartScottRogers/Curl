---
id: BL-4803
title: Rename the stale TryRead names left in ADR-1112 and ProtobufReaderTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-1112-dynamic-protobuf-messages-hold-boxed-values-by-field-and-decode-to-the-wire-codecs-errors.md, Curl.Protobuf.UnitTests/Wire/ProtobufReaderTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4803 — Rename the stale TryRead names left in ADR-1112 and ProtobufReaderTests

## Goal

No document or test name in the repository still names the `ProtobufReader.TryRead*` methods that the diff renamed to `Read*`.

## Context

Found by the shift-end review of 2026-11-03 (ADR-0710), by the type-design-analyzer, and verified against the code. The diff 77d5a82d9...4501875e6 renamed `ProtobufReader.TryReadVarint`, `TryReadFixed32`/`64`, `TryReadLengthDelimited`, `TryReadTag`, `TryReadEmbeddedMessage`, `TryReadField`, `TryReadFieldValue` and `TryReadAllFields` to `Read*` (`Curl.Protobuf.UnitLibrary/Wire/ProtobufReader.cs`). These still use the old names:
- `Documentation/Planning/Decisions/ADR-1112-dynamic-protobuf-messages-hold-boxed-values-by-field-and-decode-to-the-wire-codecs-errors.md:38` names `TryReadFieldValue`, which no longer exists.
- `Curl.Protobuf.UnitTests/Wire/ProtobufReaderTests.cs:37` has the test `TryReadFixed_TooFewBytes_IsTruncatedFixed`.
- `Curl.Protobuf.UnitTests/Wire/ProtobufReaderTests.cs:184` has the test `TryReadPacked_TruncatedElement_IsTheElementsError`.

CLAUDE.md's "say what it does" rule makes a stale name a defect: an agent reading ADR-1112 looks for a method that is not there.

## Acceptance criteria

- [ ] ADR-1112 names `ReadFieldValue` (a note saying the method was renamed from `TryReadFieldValue` is fine).
- [ ] The two tests are renamed with a `Read` prefix (`ReadFixed_...`, `ReadPacked_...`).
- [ ] `git grep -n "TryRead" -- Curl.Protobuf.* Documentation/Planning/Decisions/ADR-1112-*` finds nothing that names a `ProtobufReader` method.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-03: Created.
