---
id: BL-5967
title: Resumably upload a non-seekable -T source over 2 GiB without failing in memory
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadTransferHook.cs, Curl.Extension.ResumableUpload.UnitLibrary/Transfer/RereadableUpload.cs, Curl.Extension.ResumableUpload.UnitLibrary/CLAUDE.md, Curl.Extension.ResumableUpload.UnitTests/Transfer, Documentation/Planning/Decisions/ADR-1103-resumable-upload-picks-tus-by-options-and-resumes-from-the-servers-offset.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5967 — Resumably upload a non-seekable -T source over 2 GiB without failing in memory

## Goal

A resumable upload of a non-seekable `-T` source (`-T -`) over 2 GiB either uploads or fails with a curl exit code and message, never with an unhandled `IOException`.

## Context

- Found while doing BL-5964, which made a seekable upload stream from its source.
- `ResumableUploadTransferHook.SeekableAsync` (`Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadTransferHook.cs`) reads a non-seekable upload, or one already read part way, whole into a `MemoryStream`, which holds at most 2 GiB and throws `IOException: Stream was too long` past that; the hook maps no exit code to it.
- Both protocols announce the upload's length on creation and send again from the server's offset, so the content must be kept somewhere that can be reread, or the upload refused. Decide which and record it in an ADR.

## Acceptance criteria

- [x] A unit test in `Curl.Extension.ResumableUpload.UnitTests` shows that a non-seekable upload too large for the buffer ends as the ADR decides (uploaded, or an exit code with a message), without the test holding 2 GiB in memory.
- [x] An ADR records the choice, marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes for the ResumableUpload test project, and the library keeps 100% line and branch coverage.

## Notes

- Decided (ADR-1400): upload it. Up to 16 MiB in memory, past that a delete-on-close temporary file; exit 26 if the source cannot be read, 23 if the file cannot be written. No curl behaviour to match: resumable upload is an extension.
- A seekable upload already read part way is now sent through a window over it instead of being copied.
- `touches` widened: the new `RereadableUpload.cs` keeps the copying out of the hook, the library `CLAUDE.md` described the old behaviour, and ADR-1103 gained an "Amended by" line. No other task in Doing named them.
- Tests drive the spill with a small `memoryLimit` and an injected spool stream, so nothing near 2 GiB is held. `Curl.Extension.ResumableUpload.UnitTests`: 114 passed; library 100% line and branch, 0 failing members.
- The tests use only memory streams and `Path.GetTempFileName`, so they are platform-neutral.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 423d1023 12e4f3af c1c4a64e.
- 2026-11-25: Doing -> Done. A non-seekable -T upload of any length is resumably uploaded: past 16 MiB it spools to a temporary file; read and spool failures exit 26 and 23
