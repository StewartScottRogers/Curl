---
id: BL-3948
title: CGR-088 follow-up (HTTP part 1): echo curl's failf message as a -v line for --max-redirs, --max-filesize and -m
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr088LimitTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3948 — CGR-088 follow-up (HTTP part 1): echo curl's failf message as a -v line for --max-redirs, --max-filesize and -m

## Goal

Under -v and --trace, --max-redirs, --max-filesize (HTTP) and -m failures write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. Messages are in HttpTransferMessages.cs (RedirectLimit "Maximum (N) redirects followed", file-size and "Operation timed out after" builders); the echo sites are HttpProtocolHandler.FailedAttemptOutcome's helpers.

## Acceptance criteria

- [ ] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [ ] A focused unit test in Curl.Protocol.Http.UnitTests/Cgr088LimitTests.cs fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 1f60500d 779790c0.
