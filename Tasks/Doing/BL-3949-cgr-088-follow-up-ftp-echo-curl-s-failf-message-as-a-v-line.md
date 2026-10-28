---
id: BL-3949
title: CGR-088 follow-up (FTP): echo curl's failf message as a -v line for STOR and REST errors, a 500 greeting and 226 replaced by 451
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs, Curl.Protocol.Ftp.UnitTests/Cgr088FollowUpTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3949 — CGR-088 follow-up (FTP): echo curl's failf message as a -v line for STOR and REST errors, a 500 greeting and 226 replaced by 451

## Goal

Under -v and --trace, FTP STOR and REST errors, a 500 greeting and a 226 replaced by 451 write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. BL-3617 echoed FTP 530/550/QUOT through FtpSession.QuitAndFailAsync and FtpTransferMessages.cs; extend the same sites.

## Acceptance criteria

- [ ] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [ ] A focused unit test in Curl.Protocol.Ftp.UnitTests/Cgr088FollowUpTests.cs fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 44728195 779790c0.
