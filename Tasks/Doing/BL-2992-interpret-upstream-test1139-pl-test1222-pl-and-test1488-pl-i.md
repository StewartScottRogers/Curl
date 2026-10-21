---
id: BL-2992
title: Interpret upstream test1139.pl, test1222.pl and test1488.pl in C# over Libcurl's option table and the vendored libcurl man pages so cases 1139, 1222 and 1488 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2990]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1139Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1222Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1488Script.cs, Curl.Conformance.UnitTests/UpstreamTest1139ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1222ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1488ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1139.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1222.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1488.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2992 — Interpret upstream test1139.pl, test1222.pl and test1488.pl in C# over Libcurl's option table and the vendored libcurl man pages so cases 1139, 1222 and 1488 run

## Goal

Upstream cases 1139, 1222 and 1488 run through C# interpretations of curl 8.22.0's `tests/test1139.pl` (every libcurl option has a man page), `test1222.pl` (deprecation statuses and versions) and `test1488.pl` (symbols-in-versions and man pages agree on added-in versions), with `Libcurl`'s tables standing for the C headers, and pass.

## Context

Split from BL-2851 (2026-10-21, lane 7); the vendored docs come from BL-2990. Follow the pattern of `UpstreamTest971Script.cs` (BL-2865): one script class per file, registered from its own `UpstreamPerlCommandScripts.Test<N>.cs` file (BL-2994), a focused test class per script. Where a check finds `Libcurl` differing from upstream, fix `Libcurl` with a focused unit test in `Libcurl.UnitTests` (ADR-0482) and add those files to `touches`. Before code, run each case with `$env:CURL_UPSTREAM_CASE` and confirm it reaches the perl dispatch rather than being skipped for another reason.

## Acceptance criteria

- [ ] Cases 1139, 1222 and 1488 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Each script class has focused unit tests, and any `Libcurl` fix has a focused unit test in `Libcurl.UnitTests` that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: touches narrowed by BL-2994: the Perl script registration moved out of UpstreamConformanceTests.cs into one UpstreamPerlCommandScripts.Test<N>.cs file per script.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 435f743e 9a48bfcc dd1cc079.
