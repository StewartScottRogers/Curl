---
id: BL-2993
title: Interpret upstream test1177.pl in C# over Libcurl's CurlVersionFeatures and curl_version_info.3 so case 1177 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2990]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1177Script.cs, Curl.Conformance.UnitTests/UpstreamTest1177ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2993 — Interpret upstream test1177.pl in C# over Libcurl's CurlVersionFeatures and curl_version_info.3 so case 1177 runs

## Goal

Upstream case 1177 runs through a C# interpretation of curl 8.22.0's `tests/test1177.pl`, which checks that the feature names and `CURL_VERSION_*` bits in `curl_version_info.3`, `curl.h` and `lib/version.c` agree, with `Libcurl`'s `CurlVersionFeatures` standing for `curl.h` and `version.c`, and passes.

## Context

Split from BL-2851 (2026-10-21, lane 7); the vendored `docs/libcurl/curl_version_info.3` comes from BL-2990. Follow the pattern of `UpstreamTest971Script.cs` (BL-2865). Where the check finds `Libcurl` differing, fix `Libcurl` with a focused unit test in `Libcurl.UnitTests` (ADR-0482) and add those files to `touches`. Before code, run the case with `$env:CURL_UPSTREAM_CASE='1177'` and confirm it reaches the perl dispatch.

## Acceptance criteria

- [ ] Case 1177 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1177'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] The script class has focused unit tests, and any `Libcurl` fix has a focused unit test in `Libcurl.UnitTests` that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
