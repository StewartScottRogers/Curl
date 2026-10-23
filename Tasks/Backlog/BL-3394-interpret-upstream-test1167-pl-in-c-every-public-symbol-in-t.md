---
id: BL-3394
title: Interpret upstream test1167.pl in C#: every public symbol in the headers has the curl prefix (case 1167)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1167.cs, Curl.Conformance.UnitTests/UpstreamTest1167ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed:
---
# BL-3394 — Interpret upstream test1167.pl in C#: every public symbol in the headers has the curl prefix (case 1167)

## Goal

Upstream case 1167 runs through a C# interpretation of curl 8.22.0's `tests/test1167.pl` (every public symbol in the headers carries the curl prefix), reading the vendored upstream files, and passes on Windows with the case on the ratchet.

## Context

Decided in BL-3406 (supersedes the no-counterpart row of ADR-0718/ADR-0755): like ADR-0749's cases 1119, 1175 and 1477, this case checks upstream's tree for consistency, and the vendored files under `Curl.Conformance.UnitTests/UpstreamTestData` are that tree. Follow the pattern of `UpstreamTest1119Script.cs` (ADR-0749) and `UpstreamTest971Script.cs`: one script class in `Curl.Conformance.UnitLibrary`, registered by a module initializer in its own `UpstreamPerlCommandScripts.Test<N>.cs` (ADR-0714, BL-2994), a focused test class. Script source: https://github.com/curl/curl/blob/curl-8_22_0/tests/test1167.pl. Inputs: `include/curl/*.h` (already vendored). The case's command line is in `UpstreamTestData/test1167.curltest`. First run it with `$env:CURL_UPSTREAM_CASE='1167'` and confirm it reaches the perl dispatch rather than skipping for another reason. Where the script prints a message upstream and exits 0, the interpretation prints the same bytes.

## Acceptance criteria

- [ ] Case 1167 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1167'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] The script class has focused unit tests in `Curl.Conformance.UnitTests` that pass on the vendored files, and fail (shown by a deliberately broken input) for at least one violation the script reports.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method.

## Notes

## Log

- 2026-10-23: Created.
