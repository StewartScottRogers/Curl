---
id: BL-3396
title: Interpret upstream test1544.pl in C#: the OS/400 wrapper translates every string option (case 1544)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3391]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1544.cs, Curl.Conformance.UnitTests/UpstreamTest1544ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3396 — Interpret upstream test1544.pl in C#: the OS/400 wrapper translates every string option (case 1544)

## Goal

Upstream case 1544 runs through a C# interpretation of curl 8.22.0's `tests/test1544.pl` (every string option of curl.h is translated by the OS/400 wrapper), reading the vendored upstream files, and passes on Windows with the case on the ratchet.

## Context

Decided in BL-3406 (supersedes the no-counterpart row of ADR-0718/ADR-0755): like ADR-0749's cases 1119, 1175 and 1477, this case checks upstream's tree for consistency, and the vendored files under `Curl.Conformance.UnitTests/UpstreamTestData` are that tree. Follow the pattern of `UpstreamTest1119Script.cs` (ADR-0749) and `UpstreamTest971Script.cs`: one script class in `Curl.Conformance.UnitLibrary`, registered by a module initializer in its own `UpstreamPerlCommandScripts.Test<N>.cs` (ADR-0714, BL-2994), a focused test class. Script source: https://github.com/curl/curl/blob/curl-8_22_0/tests/test1544.pl. Inputs: `include/curl/curl.h` and `projects/OS400/ccsidcurl.c` (the latter vendored by BL-3391). The case's command line is in `UpstreamTestData/test1544.curltest`. First run it with `$env:CURL_UPSTREAM_CASE='1544'` and confirm it reaches the perl dispatch rather than skipping for another reason. Where the script prints a message upstream and exits 0, the interpretation prints the same bytes.

## Acceptance criteria

- [x] Case 1544 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1544'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] The script class has focused unit tests in `Curl.Conformance.UnitTests` that pass on the vendored files, and fail (shown by a deliberately broken input) for at least one violation the script reports.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method.

## Notes

- Case 1544 passes with CURL_UPSTREAM_CASE=1544 on Windows and is listed. The script prints its differences in hash order; the interpretation sorts them (ordinal) so output is deterministic. The case has no expected stdout, only the exit code (1 when anything is reported).
- Added Curl.Conformance.UnitTests.csproj to touches: it now copies UpstreamTestData/projects/OS400/ccsidcurl.c beside the tests (no other Doing task touches it). Tests break the input deliberately (missing, ghost and unsorted options). Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members; fast tests green; platform-neutral (no OS paths), so CI on Linux and macOS holds.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 260e5d5a c9ef8a5b b41964f0.
- 2026-10-23: Doing -> Done. Case 1544 (test1544.pl OS/400 wrapper string options) interpreted in C# and on the ratchet
