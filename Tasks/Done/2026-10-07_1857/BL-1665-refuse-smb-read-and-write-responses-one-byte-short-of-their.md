---
id: BL-1665
title: Refuse SMB read and write responses one byte short of their offset words instead of throwing
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1665 — Refuse SMB read and write responses one byte short of their offset words instead of throwing

## Goal

`SmbReadResponse.TryGetData` and `SmbWriteResponse.TryReadCount` refuse a reply that ends inside the 16-bit word they read, as an SMB refusal, instead of throwing `ArgumentOutOfRangeException` out of the handler.

## Context

- Found by BL-1516's adversarial tests. `SmbReadResponse.MinimumLength` is `SmbMessageHeader.Length + 14` (50), but the data offset word is read at bytes 49-50, so a 50-byte read reply throws. `SmbWriteResponse.MinimumLength` is `SmbMessageHeader.Length + 6` (42), but the count word is read at bytes 41-42, so a 42-byte write reply throws.
- `SmbMessageReader` accepts both frames (the word count pushes the expected size past the frame, so no byte count is checked), so a hostile server crashes `curl smb://...` with an unhandled exception instead of an exit code.
- curl 8.21.0's `smb.c` checks `smbc->got < sizeof(struct smb_header) + 14` (and `+ 6`) the same way, but reads from its 0x9000-byte receive buffer, so it reads one stale byte rather than crashing. Matching curl: take the minimum one byte higher (51 and 43) and give the refusal each parser already gives for a short reply (`Failure when receiving data from the peer`, exit 56, for a read; the upload's refusal for a write) - or read the stale byte as zero; record which in Notes.
- Boundary tests at 49/51 and 41/43 are already in `Curl.Protocol.Smb.UnitTests/SmbAdversarialTests.cs`; add the 50 and 42 rows with the fix.

## Acceptance criteria

- [x] `SmbReadResponse.TryGetData` on a 50-byte reply and `SmbWriteResponse.TryReadCount` on a 42-byte reply return a refusal and do not throw, pinned by tests in `SmbAdversarialTests`.
- [x] A handler test drives a 50-byte read reply through `SmbProtocolHandler.ExecuteAsync` and gets an exit code, not an exception.
- [x] `dotnet build` is clean and the fast tests pass; `Curl.Protocol.Smb.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Chose refusal over reading the stale byte as zero: both minimums now end at the word they read (`DataOffsetOffset + 2` = 51, `CountOffset + 2` = 43). curl reads one stale byte from its 0x9000-byte receive buffer there, which is undefined content, not behaviour to copy; a reply that ends inside a field is short, and each parser already has its short-reply refusal (exit 56 `Failure when receiving data from the peer` for a read, the upload's exit 25 refusal for a write).
- Tests: the 50 and 42 rows in `SmbAdversarialTests`, and `ExecuteAsync_ReadReplyEndingInsideTheDataOffsetWord_Exits56NotThrows`. Measure-CodeQuality: Smb 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-07: Created by BL-1516.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. SMB read and write replies that end inside their offset or count word are refused (exit 56 / upload refusal) instead of throwing
