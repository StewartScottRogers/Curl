---
id: BL-1189
title: Write the upload readers' and redirect hops' [READ] trace lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1189 — Write the upload readers' and redirect hops' [READ] trace lines

## Goal

Curl writes curl 8.21.0's `[READ]` lines for an upload body's readers and for each followed redirect hop under `--trace-config read`, `all`, `-vvv` and `-vvvv`.

## Context

- Split from BL-1159, which writes the two `[READ] client_reset, clear readers` lines of a plain transfer (`ClientReaderResetTraceEvents`, `CurlCommandRunner.TraceClientReaderReset`). Measured 2026-10-02 for `-d ab`: after `using HTTP/1.x`, `[READ] add buf reader, len=2 -> 0`, `[READ] cr_buf_read(len=65388) -> 0, nread=2, eos=1`, `[READ] client_read(len=65388) -> 0, nread=2, eos=1`, before `upload completely sent off`. `-L` hops were not measured (`Record-CurlExchange.ps1` loops answering a self-redirect; serve a distinct second response).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` for `-d`, `-T` and `-L` with one redirect; stderr in Notes.
- [ ] Tests pin each case's `[READ]` lines, and that none appears without `read` or `all`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
