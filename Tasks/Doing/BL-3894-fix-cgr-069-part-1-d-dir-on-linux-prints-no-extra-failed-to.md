---
id: BL-3894
title: Fix CGR-069 part 1: -d @dir on Linux prints no extra 'Failed to open' line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/Cgr069DataFileTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3894 — Fix CGR-069 part 1: -d @dir on Linux prints no extra 'Failed to open' line

## Goal

On Linux, `curl -sS -d @sub http://127.0.0.1:PORT/a` behaves as curl 8.22.0 does: same exit code, stdout bytes, stderr text and request bytes on the wire (CGR-069).

## Context

Part of BL-3598 (CGR-069, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Expected real curl: exits 26 with two lines: `curl: option -d: error encountered when reading a file` and the try-help line (no `curl: Failed to open sub` line). Measure curl first with `Record-CurlExchange.ps1`; re-check against 8.22.0.

## Acceptance criteria

- [ ] `curl -sS -d @sub http://127.0.0.1:PORT/a` through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes; commands and both results recorded in Notes.
- [ ] A focused unit test in the named `.UnitTests` file fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3598 (over its cost cap).
- Continues the stashed work of BL-3598.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6ed89410 a7c0fc04 779790c0.
