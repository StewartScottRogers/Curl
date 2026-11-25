---
id: BL-5964
title: Stream a resumable upload's remainder from the file instead of copying it into memory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs, Curl.Extension.ResumableUpload.UnitLibrary/Transfer/UploadRemainderStream.cs, Curl.Extension.ResumableUpload.UnitLibrary/CLAUDE.md, Curl.Extension.ResumableUpload.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5964 — Stream a resumable upload's remainder from the file instead of copying it into memory

## Goal

A resumable upload (`-T` through the ResumableUpload extension) sends each PATCH body from the upload stream at its offset, without copying the rest of the file into memory, so a file over 2 GiB uploads and memory stays flat.

## Context

- Found by the shift-end review of 2026-11-24 (pr-review-toolkit code-reviewer), commit 586f46338f (BL-5869, ADR-1379).
- `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs:83-95` (`SliceAsync`) copies everything from `offset` to the end of `body` into a new `MemoryStream` for every request, resume attempts included. The hook (`ResumableUploadTransferHook.cs:42`, `SeekableAsync`) passes a seekable upload such as a `FileStream` straight through, so the whole remainder of a file is buffered.
- Before the commit, the session set `body.Position = offset` and streamed from the file with a content length of `body.Length - offset`.
- Failure: a resumable `-T` of a 3 GiB file throws `IOException: Stream was too long` from `MemoryStream`, which the hook does not map to a curl exit code; below that, every attempt holds the remaining file in memory.

## Acceptance criteria

- [x] `SliceAsync` (or its replacement) hands the transfer a read-only window over the original stream from `offset` with length `body.Length - offset`; no `MemoryStream` copy of the body remains in `ResumableUploadSession.cs`.
- [x] A unit test in `Curl.Extension.ResumableUpload.UnitTests` shows the request body of a resumed PATCH starts at the offset and carries exactly the remaining bytes, and that the session does not read the upload stream beyond what the transfer reads.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the ResumableUpload test project; the library keeps 100% line and branch coverage.

## Notes

- `SliceAsync` is replaced by `UploadRemainderStream`, in its own file (one type per file). It is seekable because curl's HTTP handler rewinds a body by setting `Position` to resend it (417, authentication). It does not dispose the upload.
- `touches` gained that new file and the library's `CLAUDE.md`; no task in Doing on `origin/work/dark-factory` names either.
- The measurement found `ResumableUploadSession.SendAsync` at complexity 14 (gate 10), so it was split into `RequestFor` and `ExchangeOf`. Final: line 100%, branch 100%, no failing member.
- No new ADR: ADR-1379 already says the request body is "the slice of the upload from the offset", which stays true.
- `ScriptedNext` now reads the upload through `Stream` rather than casting it to `MemoryStream`.
- Verified: `dotnet build` clean, `dotnet format --verify-no-changes` clean on both projects, fast tests pass (115 assemblies; ResumableUpload 99 of 99).
- Filed BL-5967: a non-seekable upload over 2 GiB is still buffered in a `MemoryStream` by the hook.

## Log

- 2026-11-25: Created by the shift-end review (ADR-0710).
- 2026-11-25: Backlog -> Doing. Criteria at claim: d86cd4db fc85aa0d b5584880.
- 2026-11-25: Doing -> Done. A resumable upload sends each request body from the upload stream at its offset without copying it into memory.
