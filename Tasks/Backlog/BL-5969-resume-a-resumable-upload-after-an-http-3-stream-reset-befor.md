---
id: BL-5969
title: Resume a resumable upload after an HTTP/3 stream reset before the response head
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs, Curl.Extension.ResumableUpload.UnitTests/Transfer/ResumableUploadTransferHookTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5969 — Resume a resumable upload after an HTTP/3 stream reset before the response head

## Goal

A resumable upload over `--http3` whose request stream is reset or closed before the response head resumes from the server's offset instead of failing with exit 95.

## Context

- Found while delivering BL-5965 (ADR-1398), which made exits 16, 18 and 92 resumable but left HTTP/3 out of its scope.
- `Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` exits 95 (`HTTP3`) for a request stream reset before any body byte (`Reset`) and for one closed before the response head (`ReceiveEnd`); after body bytes a reset exits 18, which already resumes.
- `ResumableUploadSession.ConnectionBreaks` lists the resumable exit codes. Decide whether 95 belongs there and record it in an ADR.

## Acceptance criteria

- [ ] The decision for exit 95 is recorded in an ADR and `ResumableUploadSession.ConnectionBreaks` and its doc comment match it.
- [ ] A unit test in `Curl.Extension.ResumableUpload.UnitTests` has the transfer return exit 95 on a PATCH and shows what the ADR decides.
- [ ] `dotnet build` is clean, the fast tests of the ResumableUpload test project pass, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
