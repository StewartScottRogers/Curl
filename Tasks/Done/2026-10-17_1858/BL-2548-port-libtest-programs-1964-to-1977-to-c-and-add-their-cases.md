---
id: BL-2548
title: Port libtest programs 1964 to 1977 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2547]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2548 — Port libtest programs 1964 to 1977 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1964 to 1977 (batch 22) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 22 of 26: programs 1964, 1965, 1967, 1970, 1971, 1972, 1973, 1974, 1975, 1977. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1964, lib1965, lib1967, lib1970, lib1971, lib1972, lib1973, lib1974, lib1975, lib1977 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C; each serves one case (1964, 1965, 1967, 1970 to 1975, 1977). Ports `Lib1964`, `Lib1965`, `Lib1967`, `LibAwsSigV4S3Request` (lib1970, lib1971, lib1973, lib1974, lib1975 as `Lib1970` to `Lib1975`, registered one by one like `LibAwsSigV4Headers`; `SignedRequestOptions` the shared tail of options, `ReadNothing` the read callbacks of lib1971 and lib1975), `Lib1972` (lib1972's MIME post over the same options) and `Lib1977`; tests in `Lib1964To1977Tests.cs`. The fake URL handle gained a `GetResult` (`FakeCurlEngine.UrlGetResult`) so lib1967's failing `curl_url_get` branch can be driven.
- Choices (sensible defaults): lib1967 calls `curl_url()` without `curl_global_init`; the port runs it through `WithLibraryAsync` (the library the runner's engine needs) and treats a cleaned-up library as `curl_url()` answering NULL, as lib1921 does. lib1972's `curl_mime_init`/`curl_mime_addpart` checks and lib1977's `curl_easy_getinfo` checks are left out: Libcurl answers them on a live library and handle. lib1974 sets no `CURLOPT_HTTPHEADER`, so `SignedRequestOptions` leaves the option unset for an empty list.
- On the ratchet: 1965, 1967. Platform check: 1965 writes scheme verdicts, 1967 the URL `/x`; no path, drive letter, OS error text or certificate.
- Not on it, filed: 1964 (expected `Authorization: XXX4-HMAC-SHA256 Credential=xxx/19700101/0/127/xxx4_request, ...` at line 3, got `X-Xxx-Date: 19700101T000000Z`: no SigV4 Authorization sent) - BL-2684. 1970 to 1975 skipped (`Curl lacks the feature Debug`) - BL-2685, after BL-2675. 1977 (expected `GET /1977 HTTP/1.1`, got the end; the first perform answers 3 because nothing in the engine reads `EasyNetworkOptions.UrlHandle`) - BL-2686, after BL-2628.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run: 100% line, 100% branch, 1398 members, 0 failing, worst CRAP 10. Full build clean (0 warnings, 0 errors); fast tests green in every test project (LibTest 915, Conformance 3290 passed, 0 failed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: c55d8452 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1964 to lib1977 (ten programs) ported to C# over Libcurl; cases 1965 and 1967 on the ratchet; 1964, 1970-1975 and 1977 filed as BL-2684 to BL-2686
