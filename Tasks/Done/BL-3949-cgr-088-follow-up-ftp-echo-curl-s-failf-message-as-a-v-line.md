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
completed: 2026-10-28
---
# BL-3949 — CGR-088 follow-up (FTP): echo curl's failf message as a -v line for STOR and REST errors, a 500 greeting and 226 replaced by 451

## Goal

Under -v and --trace, FTP STOR and REST errors, a 500 greeting and a 226 replaced by 451 write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. BL-3617 echoed FTP 530/550/QUOT through FtpSession.QuitAndFailAsync and FtpTransferMessages.cs; extend the same sites.

## Acceptance criteria

- [x] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [x] A focused unit test in Curl.Protocol.Ftp.UnitTests/Cgr088FollowUpTests.cs fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes
- Measured 2026-10-28, curl 8.22.0 Schannel, `Record-CurlExchange.ps1 -Ftp -CurlArgs -sv,...`: `-T file` with `STOR=550 no` -> `* Failed FTP upload: 550`, `* Remembering we are in directory ""`, `* Uploaded unaligned file size (0 out of 3 bytes)`, `* Connection #0 ... left intact` (Curl already matched); `-C 3` with `REST=500 no` -> `* Could not use REST`, `* shutting down connection #0` (no QUIT; was missing); `GREETING=500 no` -> `* Got a 500 ftp-server response when 220 was expected`, `* closing connection #0` (was missing); `RETRDONE=451 aborted` -> `< 451`, `* server did not report OK, got 451`, `* Connection #0 ... left intact` (already matched).
- Fix: `RestartAtAsync` fails through `FailShuttingDown`; a non-220 greeting through the new `FailClosingAfterTraceEnds`, because under `--trace-config ftp` curl writes the failf line, then `done, result=8`, then `closing connection #0`. Not echoed: curl's `[FTP] FTP ended with bad error code` trace line (not emitted by Curl before either).
- Tests: Cgr088FollowUpTests (REST, greeting) fail before the change; FtpProtocolHandlerStateTraceTests' refused-greeting transcript updated (existing test file, outside `touches`; no other task needed it). Event text only, platform-neutral.
- Measure-CodeQuality.ps1 for Curl.Protocol.Ftp.UnitLibrary: 0 failing members. Fast Ftp tests green.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 44728195 779790c0.
- 2026-10-28: Doing -> Done. -v echoes curl's failf line for a refused REST and a non-220 greeting; STOR and 451 already matched
