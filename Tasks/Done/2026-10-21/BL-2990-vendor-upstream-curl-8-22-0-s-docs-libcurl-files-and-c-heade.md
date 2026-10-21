---
id: BL-2990
title: Vendor upstream curl 8.22.0's docs/libcurl files and C headers the libcurl table checks read, and reference Libcurl from the conformance harness
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1, Curl.Conformance.UnitTests/UpstreamTestData/docs/libcurl, Curl.Conformance.UnitTests/UpstreamTestData/include, Curl.Conformance.UnitTests/UpstreamTestData/README.md, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj, Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj, Curl.Conformance.UnitTests/UpstreamTestDataTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2990 — Vendor upstream curl 8.22.0's docs/libcurl files and C headers the libcurl table checks read, and reference Libcurl from the conformance harness

## Goal

The upstream files that curl 8.22.0's `tests/test1119.pl`, `test1139.pl`, `test1175.pl`, `test1177.pl`, `test1222.pl`, `test1477.pl` and `test1488.pl` open are vendored under `Curl.Conformance.UnitTests/UpstreamTestData` and copied beside the `%SRCDIR` as BL-2864 did for `docs/cmdline-opts`, and `Curl.Conformance.UnitLibrary` references `Libcurl` so script interpretations can read its tables.

## Context

Prerequisite of BL-2851 (split 2026-10-21, lane 7). Read each script at https://github.com/curl/curl/tree/curl-8_22_0/tests and list every file it opens; at least `docs/libcurl/symbols-in-versions`, `docs/libcurl/libcurl-errors.md`, `docs/libcurl/opts/*.md`, `docs/libcurl/*.md`, `docs/libcurl/curl_version_info.3` (1177 reads it from `%PWD/../docs/libcurl`) and `docs/VERSIONS.md` (already vendored). Headers (`include/curl/*.h`) are vendored only if an interpretation needs the upstream side of a comparison; where the script checks headers, its C# counterpart reads `Libcurl`'s tables instead (`Libcurl/CLAUDE.md` maps them). Extend `Update-UpstreamTestData.ps1`, the csproj copy items, the README's file list and `UpstreamTestDataTests.cs`. Check the new `ProjectReference` causes no cycle and keeps native AOT clean.

## Acceptance criteria

- [x] `Update-UpstreamTestData.ps1` vendors every file the seven scripts open from the 8.22.0 tarball, and the vendored files are committed.
- [x] The files are copied to the test output where `%SRCDIR/..` and `%PWD/..` resolve them.
- [x] `Curl.Conformance.UnitLibrary` references `Libcurl`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Vendored: docs/libcurl (104 *.md + symbols-in-versions), docs/libcurl/opts (422 *.md), include/curl (12 *.h), plus src/tool_getparam.c and src/tool_listhelp.c (test1139.pl opens both). Also touched UpstreamTestData/.gitattributes (include/** -text) and the library CLAUDE.md; no other lane task names them.
- curl_version_info.3 is generated from curl_version_info.md at upstream build time and is not in the tarball; BL-2993 reads the vendored .md. lib/version.c is not vendored: Libcurl tables stand for it. docs/cmdline-opts/curl.1 (test1139.pl) is likewise generated and absent; BL-2992 handles that.
- Fixed Update-UpstreamTestData.ps1 deleting the hand-vendored docs/cmdline-opts/curl.txt on refresh.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: d7e06177 1d747bfb b8530a0c c5f8cef6.
- 2026-10-21: Doing -> Done. Vendored docs/libcurl, include/curl and two tool sources; Libcurl referenced from the conformance harness
