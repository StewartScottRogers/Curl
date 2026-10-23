---
id: BL-3303
title: Replay the tester's easytest.py case list and printf inputs (t1.json, pf_in.json) in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3310, BL-3311]
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases, Curl.Conformance.Differential.UnitLibrary/Libcurl/Printf, Curl.Conformance.Differential.UnitTests/Libcurl/Printf, Curl.Conformance.Differential.IntegrationTests/LibcurlPrintf]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3303 — Replay the tester's easytest.py case list and printf inputs (t1.json, pf_in.json) in the differential runner

## Goal

The differential runner replays the whole of the tester's easytest.py case list (about 300 cases; BL-3237 replayed only e_in.json, the last case it wrote) and the printf inputs t1.json and pf_in.json (LibTest printf mode) against Curl's Libcurl and libcurl 8.22.0.

## Context

BL-3237 built the easy-handle runner (Curl.Conformance.Differential.UnitLibrary/Libcurl/Easy, IntegrationTests/LibcurlEasy, ADR-0792). Port easytest.py's case() calls to an embedded JSON case file under Cases/LibcurlEasyCases with names, reusing LibcurlEasyRoundReader's round shape. t1.json is a printf scenario, not an easy-handle input: replaying it needs the variadic curl_mprintf family through function pointers (a double passed as its bit pattern in an integer argument on x64 Windows). Harness: Z:\repos\Curl.difftest\2026-10-22\ (Python is reference only).

## Acceptance criteria

- [x] Every easytest.py case and the printf inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [x] A focused unit test in the fixed library's own .UnitTests project pins it (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Filed by BL-3237's differential run on 2026-10-23.
- 2026-10-23 lane 4: sized before any code and split for size: porting easytest.py's ~300 loop-generated cases is BL-3310, and the printf replay (variadic curl_msnprintf through a function pointer, a separate reader, comparer and Integration test) is BL-3311. Checked: every path easytest.py requests is already a DifferentialRoutes route, LibcurlEasyRoundReader exists, the csproj embeds Cases\**\*.json, and Libcurl's CurlPrintf.MSNPrintf exists, so neither subtask waits on anything else. Once both are Done, this task's remaining work is one combined run, its report under Notes and the ADR-0482 unit test for any fix.

- 2026-10-23 lane 4 combined run (BL-3310 and BL-3311 Done), one `dotnet test Curl.Conformance.Differential.IntegrationTests` run of LibcurlEasyDifferentialTests and LibcurlPrintfDifferentialTests with CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll (8.22.0, Schannel), 10.6 minutes:
  - easy: `cases 519 identical 479 different 40` (467 easytest.py cases, h_round and 51 header cases; 57 different at BL-3310's run, fixes since closed 17). printf: t1 1 case, 1 identical, 0 different; pf_in 35982 scenarios, 35873 identical, 0 different, 109 long double conversions not run (ADR-0794).
  - Differences already filed: CONTENT_LENGTH_DOWNLOAD_T (notmod, cl-dup-diff, cl-and-te, resume-norange, timecond-*, maxfilesize-*, proto-redir-restrict, ignore-cl*, file-dir) BL-3323; get*/status/100 BL-3301; HTTPAUTH_USED (userpwd-*, username-password, loc-userinfo) BL-3324; OS_ERRNO (rother, userpwd-unrestricted, userpwd-redirect-other, reuse-idle-close) BL-3315; post-chunked-header BL-3320; tls-notls-server NUM_CONNECTS BL-3302 (its timeout text differs only in elapsed milliseconds).
  - New differences, filed: lowspeed residual info and rftp HTTP_VERSION BL-3334; post-flag-only SIZE_UPLOAD_T BL-3335; emptyurl error text BL-3336; file-read and file-dir FILETIME BL-3337.
  - Not defects: httpver-2 and httpver-2prior (the reference build has no nghttp2, as BL-3310 recorded); rftp's "after 0 ms" vs "after 1 ms" is timing.
- No product code changed in this task (the run found differences and filed them; no fix landed here), so there is no fix for an ADR-0482 unit test to pin; the criterion is met vacuously, with each filed task carrying its own ADR-0482 criterion. Likewise no library changed, so Measure-CodeQuality.ps1 has no changed library to report on (BL-3310 and BL-3311 each recorded 0 failing members for Curl.Conformance.Differential.UnitLibrary). Built Curl.Conformance.Differential.IntegrationTests with -warnaserror: 0 errors; Curl.Conformance.Differential.UnitTests fast tests: 591 passed. Only task files changed, so CI on Linux and macOS is unaffected.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a81919c1 ebc469ce 0ac4089b.
- 2026-10-23: Doing -> Backlog. Split for size into BL-3310 (easytest.py case list) and BL-3311 (printf replay); waits on both
- 2026-10-23: Backlog -> Doing. Criteria at claim: a81919c1 ebc469ce 0ac4089b.
- 2026-10-23: Doing -> Done. One differential run replays all 467 easytest.py cases (519 with header cases, 479 identical) and t1/pf_in printf inputs (0 different); new differences filed as BL-3334 to BL-3337
