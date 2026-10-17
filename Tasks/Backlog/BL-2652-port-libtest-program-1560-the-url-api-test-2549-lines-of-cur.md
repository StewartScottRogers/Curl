---
id: BL-2652
title: Port libtest program 1560 (the URL API test, 2549 lines of curl_url tables) to C# and add case 1560 to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2652 — Port libtest program 1560 (the URL API test, 2549 lines of curl_url tables) to C# and add case 1560 to the ratchet

## Goal

libtest program lib1560 is ported to C# and upstream case 1560 passes through the runner and is on the ratchet.

## Context

Split out of BL-2541 (batch 15 of the libtest ports, phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506) before any code, as BL-2642 was out of BL-2538. Upstream curl 8.22.0's `tests/libtest/lib1560.c` is 2549 lines: about 480 table rows (`get_parts_list`, `get_url_list`, `setget_parts_list`, `set_parts_list`, `set_url_list`, `append_list`, `clear_url_list`) driven through `curl_url_set`/`curl_url_get` by `checkparts`, `checkurl`, `updateurl`, `set_url`, `setget_parts`, `set_parts`, `get_url`, `get_parts`, `append`, `scopeid`, `get_nothing`, `clear_url`, `huge`, `urldup`, `test_api_errors` and `test_scheme_guess`, which print a line for each mismatch. The port is over `Libcurl`'s `CurlUrlHandle`; each row Libcurl answers differently is filed as its own Libcurl URL task rather than fixed here. Split the tables across several files if one grows too large.

## Acceptance criteria

- [ ] lib1560 is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in `LibTestPrograms`, with a focused unit test in `Curl.Conformance.LibTest.UnitTests`.
- [ ] Case 1560 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, when it fails, its differing rows are filed as Libcurl tasks with the board script.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
