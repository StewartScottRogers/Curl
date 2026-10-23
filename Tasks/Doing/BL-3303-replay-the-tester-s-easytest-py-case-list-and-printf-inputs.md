---
id: BL-3303
title: Replay the tester's easytest.py case list and printf inputs (t1.json, pf_in.json) in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases, Curl.Conformance.Differential.UnitLibrary/Libcurl/Printf, Curl.Conformance.Differential.UnitTests/Libcurl/Printf, Curl.Conformance.Differential.IntegrationTests/LibcurlPrintf]
requirement: none
created: 2026-10-23
completed:
---
# BL-3303 — Replay the tester's easytest.py case list and printf inputs (t1.json, pf_in.json) in the differential runner

## Goal

The differential runner replays the whole of the tester's easytest.py case list (about 300 cases; BL-3237 replayed only e_in.json, the last case it wrote) and the printf inputs t1.json and pf_in.json (LibTest printf mode) against Curl's Libcurl and libcurl 8.22.0.

## Context

BL-3237 built the easy-handle runner (Curl.Conformance.Differential.UnitLibrary/Libcurl/Easy, IntegrationTests/LibcurlEasy, ADR-0792). Port easytest.py's case() calls to an embedded JSON case file under Cases/LibcurlEasyCases with names, reusing LibcurlEasyRoundReader's round shape. t1.json is a printf scenario, not an easy-handle input: replaying it needs the variadic curl_mprintf family through function pointers (a double passed as its bit pattern in an integer argument on x64 Windows). Harness: Z:\repos\Curl.difftest\2026-10-22\ (Python is reference only).

## Acceptance criteria

- [ ] Every easytest.py case and the printf inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [ ] A focused unit test in the fixed library's own .UnitTests project pins it (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Filed by BL-3237's differential run on 2026-10-23.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a81919c1 ebc469ce 0ac4089b.
