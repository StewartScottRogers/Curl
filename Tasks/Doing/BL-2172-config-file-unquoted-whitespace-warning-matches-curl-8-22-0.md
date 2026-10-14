---
id: BL-2172
title: Config file unquoted-whitespace warning matches curl 8.22.0 (upstream test459, test1712)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-13
completed:
---
# BL-2172 — Config file unquoted-whitespace warning matches curl 8.22.0 (upstream test459, test1712)

## Goal

The warning Curl prints for a config-file option whose argument has unquoted whitespace is byte for byte curl 8.22.0's, so upstream test459 and test1712 pass.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 through the ratchet: both cases fail on `<verify><stderr>` at byte 241 of line 1; the line starts the same ("Warning: <dir>/config:1 Option 'data' uses argument with unquoted whitespace. This may ca...") and differs after it. Read the expected text in `Curl.Conformance.UnitTests/UpstreamTestData/test459.curltest` and `test1712.curltest`. The warning is written in `Curl.Cli.UnitLibrary/ConfigFileSyntax.cs`; its tests are in `Curl.Cli.UnitTests/CommandLineConfigFileTests.cs`. Debug one case with `CURL_UPSTREAM_CASE` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [ ] test459 and test1712 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Curl.Cli.UnitTests` pins the new warning text (ADR-0482).
- [ ] Upstream test320, test1062, test1498, test2059 and test2318, which BL-2160 measured passing on 2026-10-13 but unlisted, are on `PassingUpstreamCases.txt` (or, for any that no longer passes, its cause is filed as a task).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
