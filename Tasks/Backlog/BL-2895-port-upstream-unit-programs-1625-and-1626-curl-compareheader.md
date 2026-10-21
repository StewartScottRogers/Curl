---
id: BL-2895
title: Port upstream unit programs 1625 and 1626 (Curl_compareheader, Curl_copy_header_value) and split the Headers registration into one file per subarea
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/HeaderValues, Curl.Conformance.UnitProgram.UnitTests/Headers/HeaderValues, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.HeaderValues.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Netrc.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.AltSvc.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Hsts.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Http1.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Capsule.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2895 — Port upstream unit programs 1625 and 1626 (Curl_compareheader, Curl_copy_header_value) and split the Headers registration into one file per subarea

## Goal

Upstream cases 1625 and 1626 run as ported unit programs over Curl's header token match and header value extraction and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and the Headers area registers its programs from one file per subarea so the other BL-2846 subtasks run in parallel.

## Context

Split from BL-2846 for size (ten ports, file-backed stores and two parsers). curl 8.22.0 `tests/unit/unit1625.c` checks `Curl_compareheader` over 33 rows plus a 300 KiB value and prints `<n> invokes`; `unit1626.c` checks `Curl_copy_header_value` over 28 rows and prints `<n> invokes` (stdout is verified by the case). Find Curl's counterparts in `Curl.Protocol.Http.UnitLibrary` (token match on a header such as `Transfer-Encoding`/`Connection`, and header value trimming); grep found no type named after the C functions, so read the HTTP code. Mechanism: ADR-0711, `Curl.Conformance.UnitProgram.UnitLibrary/CLAUDE.md`, and the Hashes ports of BL-2842 (`UnitChecks` in `Hashes/` is reusable). First change `UnitPrograms.Headers.cs` so `HeadersPrograms` is `[.. HeaderValuesPrograms, .. NetrcPrograms, .. AltSvcPrograms, .. HstsPrograms, .. Http1Programs, .. CapsulePrograms]`, each an empty array in its own file `UnitPrograms.Headers.<Subarea>.cs`; the sibling subtasks then each touch only their own file.

## Acceptance criteria

- [ ] `UnitPrograms.Headers.cs` composes six subarea arrays, each in its own `UnitPrograms.Headers.<Subarea>.cs` file (HeaderValues, Netrc, AltSvc, Hsts, Http1, Capsule).
- [ ] unit1625 and unit1626 are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/HeaderValues/`, row for row with the C program, registered in `UnitPrograms.Headers.HeaderValues.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Headers/HeaderValues/`.
- [ ] Both cases end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

The six `UnitPrograms.Headers.<Subarea>.cs` files are created by this task and are also in `touches`.

## Log

- 2026-10-21: Created.
