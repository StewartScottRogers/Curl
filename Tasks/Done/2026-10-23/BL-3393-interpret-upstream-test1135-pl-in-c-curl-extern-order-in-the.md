---
id: BL-3393
title: Interpret upstream test1135.pl in C#: CURL_EXTERN order in the headers and the libcurl.def exports (cases 1135 and 1279)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3391]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1135Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1135.cs, Curl.Conformance.UnitTests/UpstreamTest1135ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseVerification.cs, Curl.Conformance.UnitTests/UpstreamCaseVerificationTests.cs, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3393 — Interpret upstream test1135.pl in C#: CURL_EXTERN order in the headers and the libcurl.def exports (cases 1135 and 1279)

## Goal

Upstream cases 1135 and 1279 run through a C# interpretation of curl 8.22.0's `tests/test1135.pl` (CURL_EXTERN declaration order in the public headers; with `--heading=EXPORTS --sort` (case 1279) the same list against `lib/libcurl.def`), reading the vendored upstream files, and passes on Windows with the case on the ratchet.

## Context

Decided in BL-3406 (supersedes the no-counterpart row of ADR-0718/ADR-0755): like ADR-0749's cases 1119, 1175 and 1477, this case checks upstream's tree for consistency, and the vendored files under `Curl.Conformance.UnitTests/UpstreamTestData` are that tree. Follow the pattern of `UpstreamTest1119Script.cs` (ADR-0749) and `UpstreamTest971Script.cs`: one script class in `Curl.Conformance.UnitLibrary`, registered by a module initializer in its own `UpstreamPerlCommandScripts.Test<N>.cs` (ADR-0714, BL-2994), a focused test class. Script source: https://github.com/curl/curl/blob/curl-8_22_0/tests/test1135.pl. Inputs: `include/curl/*.h` (vendored) and `lib/libcurl.def` (vendored by BL-3391). Case 1135 and case 1279 both register the script name `test1135.pl`, so one interpretation handles the `--heading=EXPORTS --sort` arguments of 1279. The case's command line is in `UpstreamTestData/test1135 and 1279.curltest`. First run it with `$env:CURL_UPSTREAM_CASE='1135 and 1279'` and confirm it reaches the perl dispatch rather than skipping for another reason. Where the script prints a message upstream and exits 0, the interpretation prints the same bytes.

## Acceptance criteria

- [x] Cases 1135 and 1279 each end `Passed` with `$env:CURL_UPSTREAM_CASE` set to them on Windows and are on `PassingUpstreamCases.txt`.
- [x] The script class has focused unit tests in `Curl.Conformance.UnitTests` that pass on the vendored files, and fail (shown by a deliberately broken input) for at least one violation the script reports.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method.

## Notes

- touches widened (no other Doing task names these): case 1279 uses `<stdout loadfile=...>`, which UpstreamCaseVerification did not support (now reads the named file, with a unit test), and `lib/libcurl.def` had to be copied beside UpstreamTestData in the test csproj, as docs/include/src are.

- Cases 1135 and 1279 pass on Windows and are listed; focused tests show the list changing with the header content (order, sorting, two-line declarations); Curl.Conformance.UnitLibrary measured 100%/100%, complexity within 10. Platform-neutral (no paths or OS text).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 0ae8cf1b c9ef8a5b b41964f0.
- 2026-10-23: Doing -> Done. Cases 1135 and 1279 pass via a C# test1135.pl interpretation and loadfile support
