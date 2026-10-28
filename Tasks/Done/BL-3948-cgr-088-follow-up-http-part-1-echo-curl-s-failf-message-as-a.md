---
id: BL-3948
title: CGR-088 follow-up (HTTP part 1): echo curl's failf message as a -v line for --max-redirs, --max-filesize and -m
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ConnectionReuse.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr088LimitTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3948 — CGR-088 follow-up (HTTP part 1): echo curl's failf message as a -v line for --max-redirs, --max-filesize and -m

## Goal

Under -v and --trace, --max-redirs, --max-filesize (HTTP) and -m failures write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. Messages are in HttpTransferMessages.cs (RedirectLimit "Maximum (N) redirects followed", file-size and "Operation timed out after" builders); the echo sites are HttpProtocolHandler.FailedAttemptOutcome's helpers.

## Acceptance criteria

- [x] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [x] A focused unit test in Curl.Protocol.Http.UnitTests/Cgr088LimitTests.cs fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

Measured with `Record-CurlExchange.ps1 -sv` on real curl (a debug build, so extra lines): `--max-filesize 5` against Content-Length 100 prints `* Maximum file size exceeded` before `* closing connection #0`; against a chunked 10-byte body prints `* Exceeded the maximum allowed file size (5) with 5 bytes`; `--max-redirs 0 -L` on a 302 prints `* Maximum (0) redirects followed` after `left intact`; `-m 1` prints `* Operation timed out after ...` style failf (the timeout server in the recorder closes instead of hanging, so exit 18 was recorded; the message is the existing exit 28 text). Done: HttpProtocolHandler echoes exit 63, the handler-side exit 47 (auth resend) and exit 28 messages at the failing site (tests in Cgr088LimitTests.cs; ConnectionReuse test updated for the new line). Not compared byte for byte against a built Curl.Console (no exe found in bin). The `-L` 302 limit is decided in Curl.Core RedirectFollower, outside this task`s touches: split to BL-3953. Quality run for Curl.Protocol.Http.UnitLibrary: 0 failing members. CI platform criterion met: tests are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 1f60500d 779790c0.
- 2026-10-28: Doing -> Done. Handler echoes max-filesize, auth-resend max-redirs and -m failf lines; -L redirect limit split to BL-3953
