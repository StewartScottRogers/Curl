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
completed:
---
# BL-3951 — CGR-088 follow-up (console): echo curl's failf message as a -v line for a bad -o path

## Goal

Under -v and --trace, an -o path that cannot be opened writes curl 8.22.0's failf message as a "* <message>" info line, in curl's order, beside its "Warning: Failed to open the file" line.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. The open failure and "client returned ERROR on write of N bytes" live in Curl.Console/DeferredOutputFileStream.cs and CurlCommandRunner.cs. OS error text differs by platform: pin each platform in its own test or keep the test to the info line.

## Acceptance criteria

- [ ] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [ ] A focused unit test in Curl.Console.UnitTests/Cgr088FollowUpTests.cs fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 ac67150d 779790c0.
