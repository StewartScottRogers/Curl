---
id: BL-2895
title: Port upstream unit programs 1625 and 1626 (Curl_compareheader, Curl_copy_header_value) and split the Headers registration into one file per subarea
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/HeaderValues, Curl.Conformance.UnitProgram.UnitTests/Headers/HeaderValues, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.HeaderValues.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Netrc.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.AltSvc.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Hsts.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Http1.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Capsule.cs, Curl.Protocol.Http.UnitLibrary/HttpHeaderLine.cs, Curl.Protocol.Http.UnitTests/HttpHeaderLineTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2895 — Port upstream unit programs 1625 and 1626 (Curl_compareheader, Curl_copy_header_value) and split the Headers registration into one file per subarea

## Goal

Upstream cases 1625 and 1626 run as ported unit programs over Curl's header token match and header value extraction and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and the Headers area registers its programs from one file per subarea so the other BL-2846 subtasks run in parallel.

## Context

Split from BL-2846 for size (ten ports, file-backed stores and two parsers). curl 8.22.0 `tests/unit/unit1625.c` checks `Curl_compareheader` over 33 rows plus a 300 KiB value and prints `<n> invokes`; `unit1626.c` checks `Curl_copy_header_value` over 28 rows and prints `<n> invokes` (stdout is verified by the case). Find Curl's counterparts in `Curl.Protocol.Http.UnitLibrary` (token match on a header such as `Transfer-Encoding`/`Connection`, and header value trimming); grep found no type named after the C functions, so read the HTTP code. Mechanism: ADR-0711, `Curl.Conformance.UnitProgram.UnitLibrary/CLAUDE.md`, and the Hashes ports of BL-2842 (`UnitChecks` in `Hashes/` is reusable). First change `UnitPrograms.Headers.cs` so `HeadersPrograms` is `[.. HeaderValuesPrograms, .. NetrcPrograms, .. AltSvcPrograms, .. HstsPrograms, .. Http1Programs, .. CapsulePrograms]`, each an empty array in its own file `UnitPrograms.Headers.<Subarea>.cs`; the sibling subtasks then each touch only their own file.

## Acceptance criteria

- [x] `UnitPrograms.Headers.cs` composes six subarea arrays, each in its own `UnitPrograms.Headers.<Subarea>.cs` file (HeaderValues, Netrc, AltSvc, Hsts, Http1, Capsule).
- [x] unit1625 and unit1626 are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/HeaderValues/`, row for row with the C program, registered in `UnitPrograms.Headers.HeaderValues.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Headers/HeaderValues/`.
- [x] Both cases end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

The six `UnitPrograms.Headers.<Subarea>.cs` files are created by this task and are also in `touches`.

- 2026-10-21 (lane 6): Curl had no counterpart of `Curl_compareheader` or `Curl_copy_header_value` (`HttpProxyTunnel.HasToken` splits on commas only and is internal to Networking). Added `Curl.Protocol.Http.UnitLibrary/HttpHeaderLine.cs` (`ListsToken`, `CopyValue`, read as curl 8.22.0 does: first line only, tokens split on blanks and commas, value after the first colon trimmed) with `Curl.Protocol.Http.UnitTests/HttpHeaderLineTests.cs`; both files added to `touches` (no task in Doing on work/dark-factory named them). Public rather than internal so the port needs no `InternalsVisibleTo` edit in the Http csproj.
- The ports share `Headers/HeaderValues/InvokeCountingUnitProgram.cs` (rows until the first failure, its description on stdout, `<n> invokes`, exit 2 `CURLE_FAILED_INIT` on a failed row); each port takes its rows by constructor so the failure branches are tested with a wrong row. unit1625 writes stdout as UTF-8 with LF line ends, as the C program's `curl_mprintf` does.
- Measured: test1625 and test1626 pass with `CURL_UPSTREAM_CASE`; quality run: Curl.Conformance.UnitProgram.UnitLibrary 100/100, max complexity 6; Curl.Protocol.Http.UnitLibrary 100/100, max complexity 10.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 2a82be6e 99a1f494 f82efa1d 50be8c6c dd1cc079.
- 2026-10-21: Doing -> Done. unit1625 and unit1626 run as ported programs over the new HttpHeaderLine and pass; Headers registers one file per subarea
