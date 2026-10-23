---
id: BL-3397
title: Port upstream unit program 1307 (Curl_fnmatch) over FtpWildcardPattern
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/Wildcards/Unit1307Fnmatch.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.ToolParsers.cs, Curl.Conformance.UnitProgram.UnitTests/Wildcards/Unit1307FnmatchTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed:
---
# BL-3397 — Port upstream unit program 1307 (Curl_fnmatch) over FtpWildcardPattern

## Goal

Upstream case 1307 runs as a ported unit program over Curl's own wildcard matcher, `FtpWildcardPattern`, instead of skipping with `Curl has no port of unit1307`, and is on the ratchet.

## Context

Unlike the C-only structures of ADR-0717, case 1307 has a Curl counterpart: BL-2476 ported curl 8.22.0's `Curl_fnmatch` (`lib/curl_fnmatch.c`) as `Curl.Protocol.Ftp.UnitLibrary/FtpWildcardPattern.cs` (ADR-0613) and pins the rows of `tests/unit/unit1307.c` in `FtpWildcardPatternTests`; BL-2480 added the Linux and macOS system-fnmatch models for the `LINUX_*` and `MAC_*` rows. ADR-0737 left 1307 off the ratchet only because upstream lists it in `tests/data/DISABLED` and skipped it as `no port of unit1307`; ADR-0711 did not give it a `no counterpart` row. Port it per ADR-0711 and ADR-0723 (a port runs the check where the platform's curl build compiles it, so the `LINUX_*`/`MAC_*` rows run on their platforms and the custom column everywhere): one `IUnitProgram` named `unit1307` in `Curl.Conformance.UnitProgram.UnitLibrary/Wildcards/`, registered in `UnitPrograms.ToolParsers.cs`, reproducing every row of https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1307.c in order and returning `unitfail`. The csproj already references `Curl.Protocol.Ftp.UnitLibrary`.

## Acceptance criteria

- [ ] `Unit1307Fnmatch` has every row of upstream's `unit1307.c` (named in its doc comment) over `FtpWildcardPattern`, is registered in `UnitPrograms.ToolParsers.cs`, and has a test in `Curl.Conformance.UnitProgram.UnitTests/Wildcards/` that runs it and expects `FailureCount` 0.
- [ ] Case 1307 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1307'` on Windows and is on `PassingUpstreamCases.txt` (the case is disabled upstream, so run it ten times and state the count in Notes, per ADR-0737's bar); if a row differs, fix `FtpWildcardPattern` with a focused test in `Curl.Protocol.Ftp.UnitTests` (ADR-0482) and add those files to `touches`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and the changed libraries keep 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 27813ec6 d2d03e5c 8b2b45e3.
