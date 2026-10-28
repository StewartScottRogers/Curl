---
id: BL-3895
title: Fix CGR-069 part 2: -T dir on Linux uploads an empty chunked body and exits 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/Cgr069UploadFileTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3895 — Fix CGR-069 part 2: -T dir on Linux uploads an empty chunked body and exits 0

## Goal

On Linux, `curl -sS -T sub http://127.0.0.1:PORT/a` behaves as curl 8.22.0 does: same exit code, stdout bytes, stderr text and request bytes on the wire (CGR-069).

## Context

Part of BL-3598 (CGR-069, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Expected real curl: sends `PUT /a` chunked with only `0

` and exits 0. Measure curl first with `Record-CurlExchange.ps1`; re-check against 8.22.0.

## Acceptance criteria

- [ ] `curl -sS -T sub http://127.0.0.1:PORT/a` through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes; commands and both results recorded in Notes.
- [ ] A focused unit test in the named `.UnitTests` file fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3598 (over its cost cap).

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: e7706e08 a7c0fc04 779790c0.
