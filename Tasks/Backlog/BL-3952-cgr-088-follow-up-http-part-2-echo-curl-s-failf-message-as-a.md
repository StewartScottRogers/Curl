---
id: BL-3952
title: CGR-088 follow-up (HTTP part 2): echo curl's failf message as a -v line for bad gzip, a bad chunk and a short body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3948]
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr088BodyTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3952 — CGR-088 follow-up (HTTP part 2): echo curl's failf message as a -v line for bad gzip, a bad chunk and a short body

## Goal

Under -v and --trace, a bad gzip body, a bad chunk and a short body write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. Messages are in HttpTransferMessages.cs ("Error while processing content unencoding: ...", "transfer closed with outstanding read data remaining", "transfer closed with N bytes remaining to read"). Depends on BL-3948 only because both edit HttpProtocolHandler.cs.

## Acceptance criteria

- [ ] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [ ] A focused unit test in Curl.Protocol.Http.UnitTests/Cgr088BodyTests.cs fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
