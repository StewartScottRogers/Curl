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
completed: 2026-11-25
---
# BL-5969 — Resume a resumable upload after an HTTP/3 stream reset before the response head

## Goal

A resumable upload over `--http3` whose request stream is reset or closed before the response head resumes from the server's offset instead of failing with exit 95.

## Context

- Found while delivering BL-5965 (ADR-1398), which made exits 16, 18 and 92 resumable but left HTTP/3 out of its scope.
- `Curl.Original.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` exits 95 (`HTTP3`) for a request stream reset before any body byte (`Reset`) and for one closed before the response head (`ReceiveEnd`); after body bytes a reset exits 18, which already resumes.
- `ResumableUploadSession.ConnectionBreaks` lists the resumable exit codes. Decide whether 95 belongs there and record it in an ADR.

## Acceptance criteria

- [x] The decision for exit 95 is recorded in an ADR and `ResumableUploadSession.ConnectionBreaks` and its doc comment match it.
- [x] A unit test in `Curl.Extension.ResumableUpload.UnitTests` has the transfer return exit 95 on a PATCH and shows what the ADR decides.
- [x] `dotnet build` is clean, the fast tests of the ResumableUpload test project pass, and the library keeps 100% line and branch coverage.

## Notes

- ADR-1399: exit 95 is resumable, as HTTP/3's counterpart of exits 92 and 16. A response head without a valid
  `:status` also exits 95 and cannot be told apart, so it is retried too, within the same resumption limit.
- Test: the `CurlExitCode.Http3` row of `ExecuteAsync_TusPatchBroken_RetrievesTheOffsetWithHeadAndPatchesFromIt`.
- Checks: build 0 warnings; ResumableUpload tests 103 passed; all fast test projects passed; format clean on the
  changed files; `Measure-CodeQuality.ps1 -Library Curl.Extension.ResumableUpload.UnitLibrary` reports 0 failing
  members. No platform-dependent behaviour is tested.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 34a2aa6b 4e0de294 e2643ee5.
- 2026-11-25: Doing -> Done. A resumable upload over HTTP/3 resumes from the server's offset after exit 95 (ADR-1399)
