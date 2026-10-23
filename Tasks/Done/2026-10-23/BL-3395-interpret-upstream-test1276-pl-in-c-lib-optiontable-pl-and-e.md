---
id: BL-3395
title: Interpret upstream test1276.pl in C#: lib/optiontable.pl and easyoptions.c agree with curl.h (case 1276)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3391]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1276Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1276.cs, Curl.Conformance.UnitTests/UpstreamTest1276ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3395 — Interpret upstream test1276.pl in C#: lib/optiontable.pl and easyoptions.c agree with curl.h (case 1276)

## Goal

Upstream case 1276 runs through a C# interpretation of curl 8.22.0's `tests/test1276.pl` (lib/optiontable.pl output against lib/easyoptions.c), reading the vendored upstream files, and passes on Windows with the case on the ratchet.

## Context

Decided in BL-3406 (supersedes the no-counterpart row of ADR-0718/ADR-0755): like ADR-0749's cases 1119, 1175 and 1477, this case checks upstream's tree for consistency, and the vendored files under `Curl.Conformance.UnitTests/UpstreamTestData` are that tree. Follow the pattern of `UpstreamTest1119Script.cs` (ADR-0749) and `UpstreamTest971Script.cs`: one script class in `Curl.Conformance.UnitLibrary`, registered by a module initializer in its own `UpstreamPerlCommandScripts.Test<N>.cs` (ADR-0714, BL-2994), a focused test class. Script source: https://github.com/curl/curl/blob/curl-8_22_0/tests/test1276.pl. Inputs: `include/curl/curl.h`, `lib/optiontable.pl` and `lib/easyoptions.c` (the last two vendored by BL-3391). The interpretation reproduces what optiontable.pl prints from curl.h in C# and compares it with the vendored easyoptions.c; it does not run Perl. The case's command line is in `UpstreamTestData/test1276.curltest`. First run it with `$env:CURL_UPSTREAM_CASE='1276'` and confirm it reaches the perl dispatch rather than skipping for another reason. Where the script prints a message upstream and exits 0, the interpretation prints the same bytes.

## Acceptance criteria

- [x] Case 1276 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1276'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] The script class has focused unit tests in `Curl.Conformance.UnitTests` that pass on the vendored files, and fail (shown by a deliberately broken input) for at least one violation the script reports.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method.

## Notes

- Added Curl.Conformance.UnitTests.csproj to touches: the vendored lib/ files (optiontable.pl, easyoptions.c) were not copied to the output, neither beside the tests nor beside UpstreamTestData where %SRCDIR/.. looks. No other Doing task names it.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 4c7c77e1 c9ef8a5b b41964f0.
- 2026-10-23: Doing -> Done. Case 1276 passes through a C# interpretation of test1276.pl and optiontable.pl over the vendored files, on the ratchet
