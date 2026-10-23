---
id: BL-3392
title: Interpret upstream test745.pl in C#: typecheck-gcc.h and curl.h stay in sync (case 745)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest745Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test745.cs, Curl.Conformance.UnitTests/UpstreamTest745ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3392 — Interpret upstream test745.pl in C#: typecheck-gcc.h and curl.h stay in sync (case 745)

## Goal

Upstream case 745 runs through a C# interpretation of curl 8.22.0's `tests/test745.pl` (typecheck-gcc.h macros against the options in curl.h), reading the vendored upstream files, and passes on Windows with the case on the ratchet.

## Context

Decided in BL-3406 (supersedes the no-counterpart row of ADR-0718/ADR-0755): like ADR-0749's cases 1119, 1175 and 1477, this case checks upstream's tree for consistency, and the vendored files under `Curl.Conformance.UnitTests/UpstreamTestData` are that tree. Follow the pattern of `UpstreamTest1119Script.cs` (ADR-0749) and `UpstreamTest971Script.cs`: one script class in `Curl.Conformance.UnitLibrary`, registered by a module initializer in its own `UpstreamPerlCommandScripts.Test<N>.cs` (ADR-0714, BL-2994), a focused test class. Script source: https://github.com/curl/curl/blob/curl-8_22_0/tests/test745.pl. Inputs: `include/curl/curl.h` and `include/curl/typecheck-gcc.h` (already vendored). The case's command line is in `UpstreamTestData/test745.curltest`. First run it with `$env:CURL_UPSTREAM_CASE='745'` and confirm it reaches the perl dispatch rather than skipping for another reason. Where the script prints a message upstream and exits 0, the interpretation prints the same bytes.

## Acceptance criteria

- [x] Case 745 ends `Passed` with `$env:CURL_UPSTREAM_CASE='745'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] The script class has focused unit tests in `Curl.Conformance.UnitTests` that pass on the vendored files, and fail (shown by a deliberately broken input) for at least one violation the script reports.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method.

## Notes

- Case 745 ran with CURL_UPSTREAM_CASE=745 and passed (full fast suite green); added to PassingUpstreamCases.txt. The broken-input criterion is met by the both-kinds-of-difference test. Linux and macOS: no paths or OS text involved, so platform-neutral. Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b35e3430 c9ef8a5b b41964f0.
- 2026-10-23: Doing -> Done. Case 745 (test745.pl typecheck-gcc.h vs curl.h) interpreted in C# and on the ratchet
