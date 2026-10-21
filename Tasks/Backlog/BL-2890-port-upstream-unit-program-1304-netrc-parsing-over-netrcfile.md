---
id: BL-2890
title: Port upstream unit program 1304 (netrc parsing) over NetrcFile
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2889]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Netrc.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Netrc, Curl.Conformance.UnitProgram.UnitTests/Headers/Netrc]
requirement: none
created: 2026-10-21
completed:
---
# BL-2890 — Port upstream unit program 1304 (netrc parsing) over NetrcFile

## Goal

Upstream case 1304 runs as a ported unit program over Curl's `NetrcFile` and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit1304.c` runs `Curl_netrc_scan` eight times over the netrc file the case writes to `%LOGDIR/netrc1304` (its path is the program's one argument, `UnitProgramContext.Arguments[0]`): unknown host, unknown login, unknown host and login, a login that is a substring and one that is a superstring of `admin`, host `example.com` with no login (admin/passwd), `curl.example.com` with no login (none/none), and `curl.example.com` with login `hilarious` (no match, no password). Counterpart: `Curl.Authentication.UnitLibrary/NetrcFile.cs` (its csproj already grants `InternalsVisibleTo` to the UnitProgram library, BL-2842). Mechanism: ADR-0711 and BL-2842's Hashes ports (`Hashes/UnitChecks`). Register in `UnitPrograms.Headers.Netrc.cs`, which BL-2889 creates.

## Acceptance criteria

- [ ] unit1304 is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Netrc/`, check for check with the C program (named in its doc comment), registered in `UnitPrograms.Headers.Netrc.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/Headers/Netrc/`.
- [ ] The case ends `Passed` with `$env:CURL_UPSTREAM_CASE='1304'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where the port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
