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
completed:
---
# BL-3311 — Replay the tester's printf inputs (t1.json, pf_in.json) against Curl's curl_msnprintf and libcurl 8.22.0 in the differential runner

## Goal

The differential runner replays the tester's printf inputs t1.json and pf_in.json (format string plus typed arguments) through Curl's curl_msnprintf and libcurl 8.22.0's curl_msnprintf and reports each scenario whose return value or output bytes differ.

## Context

Split from BL-3303 for size. Inputs: Z:\repos\Curl.difftest\2026-10-22\t1.json and pf_in.json, a JSON array of [format, [[kind, value]...]] with kinds i, l (int), q (int64), u (uint64), d (double as a 16-hex-digit bit pattern), s (string or null), p (pointer or null), c (char). The tester's LibTest/PrintfMode.cs (reference only) calls Libcurl.Misc.Printf.CurlPrintf.MSNPrintf(buf, fmt, object?[]) into a 4096-byte buffer and writes '<i> <n> <hex>'. The reference side calls libcurl-4.dll's variadic curl_msnprintf through a function pointer: on x64 Windows every variadic argument is one 8-byte slot and a double is passed in both the integer and XMM register, so pass it as its bit pattern in an integer argument (delegate* unmanaged with long slots; follow ReferenceLibcurlEasyDriver for loading the DLL). Embed the inputs as JSON under Cases (the csproj embeds Cases\**\*.json; add Cases/LibcurlPrintf to touches when filing them there).

## Acceptance criteria

- [ ] Every t1.json and pf_in.json scenario replays against both libraries in an Integration test under IntegrationTests/LibcurlPrintf; one run's identical/different counts are recorded under Notes and each new difference is filed as a task.
- [ ] The scenario reader and comparer under Libcurl/Printf have unit tests under UnitTests/Libcurl/Printf.
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9c23666e 62eca1dd 0ac4089b.
