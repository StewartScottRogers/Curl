---
id: BL-2993
title: Interpret upstream test1177.pl in C# over Libcurl's CurlVersionFeatures and curl_version_info.3 so case 1177 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2990]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1177Script.cs, Curl.Conformance.UnitTests/UpstreamTest1177ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1177.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Libcurl/Misc/Version/CurlVersion.cs, Libcurl/Misc/Version/CurlVersionFeatures.cs, Libcurl.UnitTests/Misc/Version/CurlVersionTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2993 — Interpret upstream test1177.pl in C# over Libcurl's CurlVersionFeatures and curl_version_info.3 so case 1177 runs

## Goal

Upstream case 1177 runs through a C# interpretation of curl 8.22.0's `tests/test1177.pl`, which checks that the feature names and `CURL_VERSION_*` bits in `curl_version_info.3`, `curl.h` and `lib/version.c` agree, with `Libcurl`'s `CurlVersionFeatures` standing for `curl.h` and `version.c`, and passes.

## Context

Split from BL-2851 (2026-10-21, lane 7); the vendored `docs/libcurl/curl_version_info.3` comes from BL-2990. Follow the pattern of `UpstreamTest971Script.cs` (BL-2865). Where the check finds `Libcurl` differing, fix `Libcurl` with a focused unit test in `Libcurl.UnitTests` (ADR-0482) and add those files to `touches`. Before code, run the case with `$env:CURL_UPSTREAM_CASE='1177'` and confirm it reaches the perl dispatch.

## Acceptance criteria

- [x] Case 1177 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1177'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] The script class has focused unit tests, and any `Libcurl` fix has a focused unit test in `Libcurl.UnitTests` that fails without it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- `curl_version_info.3` is generated at build time and not vendored, so a `.3` path naming no file is read as the `.md` beside it, its `## ` headings standing for the man page's `.IP` names (a real `.3`'s `.IP` lines are read too). `lib/version.c` is not vendored: `CurlVersion.FeatureNames` (new, public) stands for its `FEATURE("name"` lines and the path appears only in messages. Problems are written sorted (Perl's hash order is random); with none the script writes nothing and exits 0.
- Libcurl differed from curl 8.22.0's version.c: it listed `CharConv` (gone in 7.82.0) and `TrackMemory` (gone in 8.19.0, when CURLDEBUG became the same bit as DEBUG) and lacked the bitless names `AppleSecTrust`, `asyn-rr`, `ECH`, `HTTPSIG`, `HTTPSRR`, `NativeCA`, `proxy-HTTP3` and `SSLS-EXPORT`. Fixed the table (`Debug` now sets `CURL_VERSION_DEBUG | CURL_VERSION_CURLDEBUG`), pinned by `FeatureNames_Always_AreVersionCsFeatureNames` and the FeatureBit rows. Libcurl's three files added to `touches`: no task in Doing on origin/work/dark-factory names them.
- Measure-CodeQuality: Curl.Conformance.UnitLibrary and Libcurl both 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: touches narrowed by BL-2994: the Perl script registration moved out of UpstreamConformanceTests.cs into one UpstreamPerlCommandScripts.Test<N>.cs file per script.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 7d1ce488 3a376b5e dd1cc079.
- 2026-10-21: Doing -> Done. Case 1177 passes through a C# test1177.pl over the vendored man page, curl.h and Libcurl's corrected version.c feature names
