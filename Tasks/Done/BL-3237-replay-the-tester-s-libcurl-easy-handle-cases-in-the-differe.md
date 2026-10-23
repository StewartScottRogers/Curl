---
id: BL-3237
title: Replay the tester's Libcurl easy-handle cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Easy, Curl.Conformance.Differential.UnitTests/Libcurl/Easy, Curl.Conformance.Differential.IntegrationTests/LibcurlEasy, Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasy, Curl.Conformance.Differential.IntegrationTests/Curl.Conformance.Differential.IntegrationTests.csproj]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3237 — Replay the tester's Libcurl easy-handle cases in the differential runner

## Goal

The differential runner replays the tester's Libcurl easy-handle cases (`e_in.json`, `h_in.json`, `h_round.json`, `t1.json`) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Driver: `LibTest/EasyMode.cs`; outputs `easy_report.txt`, `h_out.txt`, `t1.out`. Reuse BL-3236's way of loading the reference DLL if it has landed, otherwise decide it here.

## Acceptance criteria

- [x] The easy-handle inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- Touches widened (no Doing task on origin/work/dark-factory names them): Cases/LibcurlEasy holds the embedded e_in.json and h_round.json (the library embeds Cases/**/*.json), and the IntegrationTests csproj gains the Libcurl reference and AllowUnsafeBlocks for the native driver.
- Decided (ADR-0792): BL-3236 had not landed, so the reference libcurl is loaded here with NativeLibrary and called through unmanaged function pointers, in process beside Curl's Libcurl; CURL_DIFFERENTIAL_REFERENCE_LIBRARY names it (inconclusive when unset). h_in.json is rebuilt from hdrtest.py's lists by LibcurlHeaderCases (51 cases, 492 queries and 36 walks each) instead of embedding 570 KB. The fixed 127.0.0.1:18080 is moved to a free port pair, as another run held 18080.
- Decided: t1.json is a printf input (LibTest printf mode), not an easy-handle one; it goes with the rest of easytest.py's case list (e_in.json held only its last case) to BL-3287.
- Run 2026-10-23 against C:Program FilesGitCrt64binibcurl-4.dll (8.22.0, Schannel, debug build), 10 minutes (e_in times out after 300 s in each library): `cases 53 identical 0 different 53`. Causes, each filed: (1) all 51 header cases and h_round: curl_header origin lacks libcurl's 1<<27 marker bit (134217729 vs 1) - BL-3284; (2) /status/100 and its follow case: reference header callback receives the 100 block and curl_easy_header answers 2, Curl delivers no headers and answers 3 - BL-3285; (3) e_in (tls-notls-server): CURLINFO_NUM_CONNECTS 1 vs 0 after the connect timeout - BL-3286. e_in's WIRE line differs only by the TLS ClientHello's random bytes (noise, as in the tester's report).
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members, 100% line and branch, worst CRAP 10 (first run flagged 4 members for complexity from LINQ lambdas; rewritten as loops). Fast tests: Curl.Conformance.Differential.UnitTests 248 passed. All new tests are pure in-memory (no paths, OS text or certificates), so platform-neutral for Linux and macOS CI.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: cd228cb8 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner replays the tester's Libcurl easy-handle cases against Curl's Libcurl and libcurl 8.22.0; differences filed as BL-3284 to BL-3286, the rest of easytest.py and the printf inputs as BL-3287
