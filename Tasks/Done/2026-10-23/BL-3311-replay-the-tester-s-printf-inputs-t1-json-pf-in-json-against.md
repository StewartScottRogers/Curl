---
id: BL-3311
title: Replay the tester's printf inputs (t1.json, pf_in.json) against Curl's curl_msnprintf and libcurl 8.22.0 in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlPrintf, Curl.Conformance.Differential.UnitLibrary/Libcurl/Printf, Curl.Conformance.Differential.UnitTests/Libcurl/Printf, Curl.Conformance.Differential.IntegrationTests/LibcurlPrintf]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3311 — Replay the tester's printf inputs (t1.json, pf_in.json) against Curl's curl_msnprintf and libcurl 8.22.0 in the differential runner

## Goal

The differential runner replays the tester's printf inputs t1.json and pf_in.json (format string plus typed arguments) through Curl's curl_msnprintf and libcurl 8.22.0's curl_msnprintf and reports each scenario whose return value or output bytes differ.

## Context

Split from BL-3303 for size. Inputs: Z:\repos\Curl.difftest\2026-10-22\t1.json and pf_in.json, a JSON array of [format, [[kind, value]...]] with kinds i, l (int), q (int64), u (uint64), d (double as a 16-hex-digit bit pattern), s (string or null), p (pointer or null), c (char). The tester's LibTest/PrintfMode.cs (reference only) calls Libcurl.Misc.Printf.CurlPrintf.MSNPrintf(buf, fmt, object?[]) into a 4096-byte buffer and writes '<i> <n> <hex>'. The reference side calls libcurl-4.dll's variadic curl_msnprintf through a function pointer: on x64 Windows every variadic argument is one 8-byte slot and a double is passed in both the integer and XMM register, so pass it as its bit pattern in an integer argument (delegate* unmanaged with long slots; follow ReferenceLibcurlEasyDriver for loading the DLL). Embed the inputs as JSON under Cases (the csproj embeds Cases\**\*.json; add Cases/LibcurlPrintf to touches when filing them there).

## Acceptance criteria

- [x] Every t1.json and pf_in.json scenario replays against both libraries in an Integration test under IntegrationTests/LibcurlPrintf; one run's identical/different counts are recorded under Notes and each new difference is filed as a task.
- [x] The scenario reader and comparer under Libcurl/Printf have unit tests under UnitTests/Libcurl/Printf.
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Decision (ADR-0794): a scenario whose format has an L (long double) float conversion is not run against the reference (counted "not run"): the tester's 109 differences were all reference access violations (EXC OSError), which would end a .NET test host. The Integration test is Windows-only ([OSCondition]) because a double is passed as its bits in an integer variadic slot, which only the x64 Windows convention reads.
- Inputs embedded as Cases/LibcurlPrintf/ZeroPaddedNan.json (t1.json) and PrintfScenarios.json (pf_in.json).
- Run 2026-10-23 against Git for Windows ucrt64/bin/libcurl-4.dll: t1 1 scenario, 1 identical, 0 different; pf_in 35982 scenarios, 35873 identical, 0 different, 109 not run. No new difference, so no task filed.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 100% line, 100% branch, 0 failing members. Unit tests are platform-neutral (no paths or OS text), so CI on Linux and macOS is covered by keeping them so.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9c23666e 62eca1dd 0ac4089b.
- 2026-10-23: Doing -> Done. The differential runner replays t1.json and pf_in.json through Curl's and libcurl 8.22.0's curl_msnprintf: 35874 identical, 0 different, 109 long double conversions not run against the crashing reference
