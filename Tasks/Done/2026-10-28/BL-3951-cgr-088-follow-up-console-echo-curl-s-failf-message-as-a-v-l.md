---
id: BL-3951
title: CGR-088 follow-up (console): echo curl's failf message as a -v line for a bad -o path
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/DeferredOutputFileStream.cs, Curl.Console.UnitTests/Cgr088FollowUpTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3951 — CGR-088 follow-up (console): echo curl's failf message as a -v line for a bad -o path

## Goal

Under -v and --trace, an -o path that cannot be opened writes curl 8.22.0's failf message as a "* <message>" info line, in curl's order, beside its "Warning: Failed to open the file" line.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. The open failure and "client returned ERROR on write of N bytes" live in Curl.Console/DeferredOutputFileStream.cs and CurlCommandRunner.cs. OS error text differs by platform: pin each platform in its own test or keep the test to the info line.

## Acceptance criteria

- [x] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [x] A focused unit test in Curl.Console.UnitTests/Cgr088FollowUpTests.cs fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured curl 8.22.0 (Schannel) `curl -v -o nodir/x.txt file:///.../s.txt` (12-byte file): meter data line, `Warning: Failed to open the file nodir/x.txt: No such file or directory`, `* client returned ERROR on write of 12 bytes`, empty line, `* closing connection #0`, `curl: (23) client returned ERROR on write of 12 bytes`. Curl.Console's exe gave the same lines after the fix (before: `* Failure writing output to destination, passed 12 returned 0` ahead of the warning, no `client returned` line). `--trace-ascii -` over a pipe orders stdout/stderr differently in real curl, so the test pins the info line, its order after the warning and the single warning for both `-v` and `--trace-ascii %`.
- Decision: ADR-0946. New `OutputFileOpenFailureTransferEvents` (events decorator, only under -v/--trace) rewrites the handler's write-failure info line at the failing site and writes the pending warning first; no handler touched. Added `RunningTransferState.OutputFile` (no other Doing task on the shared branch names it; BL-3937 touches StandardOutputOpener only).
- Tests: Curl.Console.UnitTests/Cgr088FollowUpTests.cs (fails before: the handler's line shows), OutputFileOpenFailureTransferEventsTests.cs. Platform-neutral (in-memory file system, no OS text pinned). Measure-CodeQuality.ps1 -Library Curl.Console: 0 failing members. Fast Console tests green. No option changed, so `--ai-help` is untouched.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 ac67150d 779790c0.
- 2026-10-28: Doing -> Done. A bad -o path under -v and --trace now echoes '* client returned ERROR on write of N bytes' after its Warning line, as curl 8.22.0 does
