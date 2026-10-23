---
id: BL-3236
title: Replay the tester's Libcurl URL, printf and escape scenarios (scen.json) in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Scenarios, Curl.Conformance.Differential.UnitTests/Libcurl/Scenarios, Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, Curl.Conformance.Differential.IntegrationTests/Curl.Conformance.Differential.IntegrationTests.csproj]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3236 — Replay the tester's Libcurl URL, printf and escape scenarios (scen.json) in the differential runner

## Goal

The differential runner replays the tester's Libcurl scenarios in `scen.json` (3,289: URL API `U`, printf `P`, escape `E`, `N`, `D`, version `V`, `S`) against Curl's Libcurl and the reference libcurl 8.22.0 and reports each difference.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester's C# driver is the `LibTest` folder (`Program.cs` and its modes); its output is `lib_summary.txt` and `lib_diff.txt`. Decide how the reference libcurl DLL is loaded beside Curl's (the ADR from BL-3231 or a new one). Broken IDN in the reference build is a named exclusion.

## Acceptance criteria

- [x] Every scen.json scenario kind replays against both libraries and one run's report is recorded under Notes, each new difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- 2026-10-23, lane 2: added the integration tests csproj to touches (a reference to Libcurl and the embedded scenario file); no task in Doing on origin/work/dark-factory names it.
- Design (ADR-0790): core in Libcurl/Scenarios (reader, replayer over ILibcurlScenarioTarget, report writer); the integration test replays against Curl's managed Libcurl and the reference libcurl-4.dll loaded with NativeLibrary from CURL_DIFFERENTIAL_REFERENCE_LIBCURL; scen.json committed as LibcurlScenarios/LibcurlScenarios.json. The "printf" in the title is the tester's P (URL part) kind; pf_in.json printf inputs belong to BL-3239.
- Default taken: U lines report only their differing ` |` fields, since the tester's 700-character cut hid every U difference past the first few parts.
- Run 2026-10-23 against Git for Windows ucrt64 libcurl 8.22.0: scenarios 3244 compared, 45 excluded (BrokenIdn), 113 differing - V 1/1, S 0/1, U 87/1386, P 16/1734, E 0/12, N 0/19, D 9/91. Every difference the tester listed for scheme case, `http:a`, `http:\a\b` and the like is gone.
  - U 591, 598, 605 (`1ab://`, `-ab://`, `+ab://` with CURLU_DEFAULT_SCHEME): filed BL-3289.
  - U 744-806 (file URLs, CURLUPART_PORT with CURLU_DEFAULT_PORT gives 15, libcurl "0"): filed BL-3290.
  - P 1730-1733, 2023-2026, 2769-2772, 2902-2905 (file URL without path, libcurl `file://(nil)`): filed BL-3291.
  - U 1024-1044 (255-character host label with CURLU_PUNY2IDN, libcurl 21): filed BL-3292.
  - U 1080-1100 (CURLU_URLDECODE of %FF, %C0%AF, %ED%A0%80 comes back as U+FFFD): filed BL-3293.
  - D 3207, 3211, 3212, 3229, 3230, 3283, 3286-3288: the reference's 32-bit time_t only (ThirtyTwoBitTimeT); not filed.
  - V: the reference build lists zlib, brotli, zstd, libidn2, libpsl, libssh2 and WinLDAP after Schannel; a property of that build, not filed.
- Fast tests: Curl.Conformance.Differential.UnitTests green on Windows; every new unit test uses in-memory data only, so it is platform-neutral (Linux and macOS CI).

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 714b26b1 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner replays scen.json against Curl's Libcurl and the reference libcurl 8.22.0 and writes the lib_diff report
