---
id: BL-3272
title: Answer -1 for CURLINFO_CONTENT_LENGTH_DOWNLOAD_T after a file:// upload
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Curl.Engine.UnitTests/TransferInfoLengthAndConditionTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3272 — Answer -1 for CURLINFO_CONTENT_LENGTH_DOWNLOAD_T after a file:// upload

## Goal

After a `CURLOPT_UPLOAD` transfer to a `file://` URL, `CURLINFO_CONTENT_LENGTH_DOWNLOAD_T` answers what libcurl 8.22.0 answers (-1 per its `file_upload`, which never sets a download size), not the number of bytes uploaded.

## Context

Found by the shift-end review (ADR-0710) of 6b344d38b...af2f6a9bd, code-reviewer.

`Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs` `FileLengthOf` (line 46-55) returns `result.BytesTransferred` for any successful `file://` transfer that has no `Content-Length` pseudo-header. On the upload path (`FileProtocolHandler.UploadAsync` returns `TransferResult.Success(transferred)`) that is the byte count written to the destination, so the download length reports the upload size. The new tests in `TransferInfoLengthAndConditionTests.cs` cover downloads only.

## Acceptance criteria

- [ ] libcurl 8.22.0's answer for `CURLINFO_CONTENT_LENGTH_DOWNLOAD_T` after a `file://` upload is measured (or read from upstream `lib/file.c`) and recorded in the test's comment.
- [ ] `FileLengthOf` takes the file-length branch only for downloads and answers the measured value for uploads.
- [ ] A focused unit test in `Curl.Engine.UnitTests` pins the upload case; the existing download tests still pass.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
