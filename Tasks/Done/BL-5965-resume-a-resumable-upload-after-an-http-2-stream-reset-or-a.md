---
id: BL-5965
title: Resume a resumable upload after an HTTP/2 stream reset or a cut-off response
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs, Curl.Extension.ResumableUpload.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5965 — Resume a resumable upload after an HTTP/2 stream reset or a cut-off response

## Goal

A resumable upload interrupted by an HTTP/2 stream reset, an HTTP/2 framing failure or a connection cut part-way through the response resumes from the server's offset instead of failing outright.

## Context

- Found by the shift-end review of 2026-11-24 (pr-review-toolkit silent-failure-hunter), commit 586f46338f (BL-5869, ADR-1379).
- `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs:48`: `CanResume` accepts only exit 52, 55 and 56. Before the commit the extension used its own HTTP/1.1 client, where every broken connection came back as a resumable 55 or 56. Now each request goes through curl's own transfer, which on `https` usually negotiates HTTP/2, and a reset stream exits 92 (`HTTP2_STREAM`), a framing failure 16 (`HTTP2`), a body cut short 18 (`PARTIAL_FILE`). None of those is resumable, so `ResumableUploadFlow.cs:66` gives up on exactly the interruption the extension exists to survive.
- Decide (ADR under Stewart's delegation if needed) which of 16, 18 and 92 mean "the connection broke" for the resumable-upload protocol, and add those.

## Acceptance criteria

- [x] `CanResume` treats an HTTP/2 stream reset (exit 92) as resumable, and the decision for exits 16 and 18 is recorded in the code's doc comment or an ADR.
- [x] A unit test in `Curl.Extension.ResumableUpload.UnitTests` has the transfer return exit 92 on a PATCH and shows the flow sends a HEAD for the offset and resumes, ending with exit 0.
- [x] `dotnet build` is clean and the fast tests of the ResumableUpload test project pass; the library keeps 100% line and branch coverage.

## Notes

- ADR-1394: exits 16, 18 and 92 are all resumable, beside 52, 55 and 56. 16 is what a connection closed before the final HTTP/2 response head gives; 18 is a response cut off part-way. Retries stay bounded by the resumption limit.
- The codes are a `ConnectionBreaks` list that `CanResume` looks in: as one `is ... or` pattern it measured complexity 12, over the gate of 10.
- Test: the `Http2Stream` row (and `Http2`, `PartialFile` rows) of `ExecuteAsync_TusPatchBroken_RetrievesTheOffsetWithHeadAndPatchesFromIt`.
- Verified: build clean; all fast tests exit 0; after the list refactor, build clean again and the 102 ResumableUpload tests pass; `Measure-CodeQuality.ps1 -Library Curl.Extension.ResumableUpload.UnitLibrary` gives 100% line, 100% branch, 0 failing members. The test uses a scripted transfer only, so it is platform-neutral.
- Filed BL-5969 for HTTP/3's exit 95, outside this task's three codes.

## Log

- 2026-11-25: Created by the shift-end review (ADR-0710).
- 2026-11-25: Backlog -> Doing. Criteria at claim: e47344f9 e4cbea69 922c5bba.
- 2026-11-25: Doing -> Done. A resumable upload now resumes after exit 16, 18 and 92 as well as 52, 55 and 56 (ADR-1394).
