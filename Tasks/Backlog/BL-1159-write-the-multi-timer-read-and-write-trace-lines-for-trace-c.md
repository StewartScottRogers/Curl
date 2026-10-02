---
id: BL-1159
title: Write the [MULTI], [TIMER], [READ] and [WRITE] trace lines for --trace-config, -vvv and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1103]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1159 — Write the [MULTI], [TIMER], [READ] and [WRITE] trace lines for --trace-config, -vvv and -vvvv

## Goal

Curl writes curl 8.21.0's `[MULTI]`, `[TIMER]`, `[READ]` and `[WRITE]` lines under `--trace-config multi`, `timer`, `read`, `write`, `network`, `all`, `-vvv` (`read`, `write`) and `-vvvv`.

## Context

- Split from BL-1103 (ADR-0357); `CommandLineOptions.TraceComponents` already carries `read` and `write` at `-vvv` and `all` at `-vvvv`. BL-1103's Notes hold curl's measured stderr for `multi`, `read`, `write`, `timer`, `network`, `-vvv` and `-vvvv`.
- These lines come from curl's transfer engine (state changes `[INIT] -> [SETUP]`, `[PGRS-*] added <n>ns`, the client writer stack's `[WRITE] [OUT] wrote 17 header bytes -> 17`), so they are written from where Curl runs a transfer (`Curl.Core`, the HTTP handler and the console runner); split again per component with task-planner if one run cannot hold it. Refine `touches` before starting.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` for each component named in the title (a plain HTTP transfer, a refused connect, and `localhost` where both families answer); stderr in Notes.
- [ ] Tests pin each component's stable lines for a plain HTTP transfer, and that no line appears without its component; an ADR-0357 amendment records how the volatile values (fd numbers, nanosecond stamps, poll repetitions) are produced.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
