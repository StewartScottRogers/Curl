---
id: BL-5967
title: Resumably upload a non-seekable -T source over 2 GiB without failing in memory
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadTransferHook.cs, Curl.Extension.ResumableUpload.UnitTests/Transfer]
requirement: none
created: 2026-11-25
completed:
---
# BL-5967 — Resumably upload a non-seekable -T source over 2 GiB without failing in memory

## Goal

A resumable upload of a non-seekable `-T` source (`-T -`) over 2 GiB either uploads or fails with a curl exit code and message, never with an unhandled `IOException`.

## Context

- Found while doing BL-5964, which made a seekable upload stream from its source.
- `ResumableUploadTransferHook.SeekableAsync` (`Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadTransferHook.cs`) reads a non-seekable upload, or one already read part way, whole into a `MemoryStream`, which holds at most 2 GiB and throws `IOException: Stream was too long` past that; the hook maps no exit code to it.
- Both protocols announce the upload's length on creation and send again from the server's offset, so the content must be kept somewhere that can be reread, or the upload refused. Decide which and record it in an ADR.

## Acceptance criteria

- [ ] A unit test in `Curl.Extension.ResumableUpload.UnitTests` shows that a non-seekable upload too large for the buffer ends as the ADR decides (uploaded, or an exit code with a message), without the test holding 2 GiB in memory.
- [ ] An ADR records the choice, marked "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes for the ResumableUpload test project, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
