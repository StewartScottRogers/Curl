---
id: BL-5961
title: Stream a resumable upload's remainder from the file instead of copying it into memory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs, Curl.Extension.ResumableUpload.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-5961 — Stream a resumable upload's remainder from the file instead of copying it into memory

## Goal

A resumable upload (`-T` through the ResumableUpload extension) sends each PATCH body from the upload stream at its offset, without copying the rest of the file into memory, so a file over 2 GiB uploads and memory stays flat.

## Context

- Found by the shift-end review of 2026-11-24 (pr-review-toolkit code-reviewer), commit 586f46338f (BL-5869, ADR-1379).
- `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs:83-95` (`SliceAsync`) copies everything from `offset` to the end of `body` into a new `MemoryStream` for every request, resume attempts included. The hook (`ResumableUploadTransferHook.cs:42`, `SeekableAsync`) passes a seekable upload such as a `FileStream` straight through, so the whole remainder of a file is buffered.
- Before the commit, the session set `body.Position = offset` and streamed from the file with a content length of `body.Length - offset`.
- Failure: a resumable `-T` of a 3 GiB file throws `IOException: Stream was too long` from `MemoryStream`, which the hook does not map to a curl exit code; below that, every attempt holds the remaining file in memory.

## Acceptance criteria

- [ ] `SliceAsync` (or its replacement) hands the transfer a read-only window over the original stream from `offset` with length `body.Length - offset`; no `MemoryStream` copy of the body remains in `ResumableUploadSession.cs`.
- [ ] A unit test in `Curl.Extension.ResumableUpload.UnitTests` shows the request body of a resumed PATCH starts at the offset and carries exactly the remaining bytes, and that the session does not read the upload stream beyond what the transfer reads.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the ResumableUpload test project; the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created by the shift-end review (ADR-0710).
