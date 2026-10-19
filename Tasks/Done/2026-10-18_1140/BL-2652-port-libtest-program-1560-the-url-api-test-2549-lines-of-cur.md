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
completed: 2026-10-18
---
# BL-2652 — Port libtest program 1560 (the URL API test, 2549 lines of curl_url tables) to C# and add case 1560 to the ratchet

## Goal

libtest program lib1560 is ported to C# and upstream case 1560 passes through the runner and is on the ratchet.

## Context

Split out of BL-2541 (batch 15 of the libtest ports, phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506) before any code, as BL-2642 was out of BL-2538. Upstream curl 8.22.0's `tests/libtest/lib1560.c` is 2549 lines: about 480 table rows (`get_parts_list`, `get_url_list`, `setget_parts_list`, `set_parts_list`, `set_url_list`, `append_list`, `clear_url_list`) driven through `curl_url_set`/`curl_url_get` by `checkparts`, `checkurl`, `updateurl`, `set_url`, `setget_parts`, `set_parts`, `get_url`, `get_parts`, `append`, `scopeid`, `get_nothing`, `clear_url`, `huge`, `urldup`, `test_api_errors` and `test_scheme_guess`, which print a line for each mismatch. The port is over `Libcurl`'s `CurlUrlHandle`; each row Libcurl answers differently is filed as its own Libcurl URL task rather than fixed here. Split the tables across several files if one grows too large.

## Acceptance criteria

- [x] lib1560 is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in `LibTestPrograms`, with a focused unit test in `Curl.Conformance.LibTest.UnitTests`.
- [x] Case 1560 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, when it fails, its differing rows are filed as Libcurl tasks with the board script.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- The tables (534 rows) were converted from `lib1560.c` at tag `curl-8_22_0` by a throwaway PowerShell converter, row for row, into `Lib1560Tables.*.cs`. Preprocessor choices, matching the Windows Schannel build: `USE_IDN` rows in (the engine has IDN through `IdnMapping`), its `#else` rows out; `CURL_DISABLE_WEBSOCKETS` and `CURL_DISABLE_LDAP` rows in; `_WIN32` rows carried as `WindowsOnly` and run when `OperatingSystem.IsWindows()`. C bytes that do not decode as UTF-8 (`\xc0`, `\xff`) are written as Latin-1 characters, since `CurlUrlHandle` takes .NET strings; BL-2738 decides that mapping in the engine.
- `has_utf8` is `CURL_TEST_HAVE_CODESET_UTF8` read through `curl_getenv`, as upstream; the runner does not set it, so the `CURLU_PUNYCODE`/`CURLU_PUNY2IDN` rows are skipped, as on an upstream runner without a UTF-8 codeset.
- `test_api_errors`' `NULL` handle and `NULL` part-pointer checks have no C# form (a `CurlUrlHandle` method cannot be called on `null`, and `out` cannot be `NULL`); only its unknown-part check is ported. `updateurl`'s 79-byte `sscanf` limits are left out: no row has a part or value that long.
- Case 1560 run through the runner fails today: `get_url` stops first (exit 3). A per-row run of every table against the real engine found 55 differing rows, filed by cause as BL-2731 (backslash), BL-2732 (bad scheme), BL-2733 (unsupported scheme), BL-2734 (empty host, no authority), BL-2735 (URL encoding), BL-2736 (host normalising), BL-2737 (`file:` URLs), BL-2738 (bytes that are not UTF-8), BL-2739 (part updates) and BL-2740 (blank and fragment redirects); BL-2741 holds case 1560 on the ratchet once they are Done. `scheme_guess`, `api_errors`, `urldup`, `setget_parts`, `get_nothing`, `scopeid`, `append` and `clear_url` already pass.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: e268d4d8 c7d31a42 e39e1cd8 4272a6b9.
- 2026-10-18: Doing -> Done. lib1560 is ported over CurlUrlHandle with its 534 table rows; its 55 differing rows are filed as BL-2731 to BL-2740, and BL-2741 holds case 1560 once they pass
