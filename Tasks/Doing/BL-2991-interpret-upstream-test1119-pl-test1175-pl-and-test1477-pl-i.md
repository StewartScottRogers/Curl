---
id: BL-2991
title: Interpret upstream test1119.pl, test1175.pl and test1477.pl in C# over Libcurl's symbol and error tables so cases 1119, 1175 and 1477 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2990]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1119Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1175Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1477Script.cs, Curl.Conformance.UnitTests/UpstreamTest1119ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1175ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1477ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1119.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1175.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1477.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2991 — Interpret upstream test1119.pl, test1175.pl and test1477.pl in C# over Libcurl's symbol and error tables so cases 1119, 1175 and 1477 run

## Goal

Upstream cases 1119, 1175 and 1477 run through C# interpretations of curl 8.22.0's `tests/test1119.pl` (symbols-in-versions against the headers), `test1175.pl` (symbols-in-versions against `libcurl-errors`) and `test1477.pl` (header error codes against `libcurl-errors`), with `Libcurl`'s tables (`CurlOptions.All`, `CurlCode` and its strerror table, the `CURLINFO_*` list) standing for the C headers, and pass.

## Context

Split from BL-2851 (2026-10-21, lane 7); the vendored docs come from BL-2990. Follow the pattern of `UpstreamTest971Script.cs` (BL-2865): one self-registering script class per file, registered from its own `UpstreamPerlCommandScripts.Test<N>.cs` file (BL-2994), a focused test class per script. Where a check finds `Libcurl` differing from upstream, fix `Libcurl` with a focused unit test in `Libcurl.UnitTests` (ADR-0482) and add those files to `touches`. Before code, run each case with `$env:CURL_UPSTREAM_CASE` and confirm it reaches the perl dispatch rather than being skipped for another reason.

## Acceptance criteria

- [ ] Cases 1119, 1175 and 1477 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Each script class has focused unit tests, and any `Libcurl` fix has a focused unit test in `Libcurl.UnitTests` that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: touches narrowed by BL-2994: the Perl script registration moved out of UpstreamConformanceTests.cs into one UpstreamPerlCommandScripts.Test<N>.cs file per script.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 5fec9bf1 9a48bfcc dd1cc079.
