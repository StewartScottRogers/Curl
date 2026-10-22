---
id: BL-2896
title: Port upstream unit program 1304 (netrc parsing) over NetrcFile
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2895]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Netrc.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Netrc, Curl.Conformance.UnitProgram.UnitTests/Headers/Netrc]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2896 — Port upstream unit program 1304 (netrc parsing) over NetrcFile

## Goal

Upstream case 1304 runs as a ported unit program over Curl's `NetrcFile` and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit1304.c` runs `Curl_netrc_scan` eight times over the netrc file the case writes to `%LOGDIR/netrc1304` (its path is the program's one argument, `UnitProgramContext.Arguments[0]`): unknown host, unknown login, unknown host and login, a login that is a substring and one that is a superstring of `admin`, host `example.com` with no login (admin/passwd), `curl.example.com` with no login (none/none), and `curl.example.com` with login `hilarious` (no match, no password). Counterpart: `Curl.Authentication.UnitLibrary/NetrcFile.cs` (its csproj already grants `InternalsVisibleTo` to the UnitProgram library, BL-2842). Mechanism: ADR-0711 and BL-2842's Hashes ports (`Hashes/UnitChecks`). Register in `UnitPrograms.Headers.Netrc.cs`, which BL-2895 creates.

## Acceptance criteria

- [x] unit1304 is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Netrc/`, check for check with the C program (named in its doc comment), registered in `UnitPrograms.Headers.Netrc.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/Headers/Netrc/`.
- [x] The case ends `Passed` with `$env:CURL_UPSTREAM_CASE='1304'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where the port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Ported from curl-8_22_0 `tests/unit/unit1304.c` (fetched from GitHub; not in the tree). Its eight `Curl_netrc_scan` calls map to `NetrcFile.Find`: `NETRC_NO_MATCH` is `NetrcLookupOutcome.NotFound`, `NETRC_OK` is `Found`, and `Curl_creds_user`/`Curl_creds_passwd` (empty for no creds) are `Login ?? ""`/`Password ?? ""`. Failures are written through the existing `UnitChecks` (`unit1304: <message>`), as the Hashes ports do.
- The port found Curl right: no product fix, so the ADR-0482 criterion needs no extra test and no extra `touches`.
- Measured: case 1304 `Passed` with `CURL_UPSTREAM_CASE=1304`; `Curl.Conformance.UnitProgram.UnitLibrary` 100% line, 100% branch, 0 failing members (Measure-CodeQuality.ps1).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: ff74b0c9 b8d1c68f d0b9baa3 dd1cc079.
- 2026-10-21: Doing -> Done. Upstream unit program 1304 (netrc parsing) runs as a port over NetrcFile and case 1304 is on the ratchet
