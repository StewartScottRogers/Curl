---
id: BL-2848
title: Port upstream unit programs for time, speed and size limits: cases 1303, 1323, 1399, 1605, 1606, 3214 and 3216
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits, Curl.Conformance.UnitProgram.UnitTests/Limits]
requirement: none
created: 2026-10-19
completed:
---
# BL-2848 — Port upstream unit programs for time, speed and size limits: cases 1303, 1323, 1399, 1605, 1606, 3214 and 3216

## Goal

Upstream cases 1303, 1323, 1399, 1605, 1606, 3214 and 3216 run as ported unit programs over Curl's timeout, progress, speed-limit and rate-limit code, or have a reason recorded, and those that pass are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

These seven `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one area each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit): 1303 `Curl_timeleft_ms`, 1323 `curlx_tvdiff`, 1399 `Curl_pgrsTime`, 1605 negative data lengths as input to libcurl functions, 1606 speedcheck, 3214 struct size checks and 3216 ratelimit. Curl's counterparts: the timeout logic in `Curl.Core.UnitLibrary`/`Curl.Engine.UnitLibrary` (transfer timers over `TimeProvider`, root CLAUDE.md: never `Thread.Sleep`), the progress and rate-limit code in `Curl.Output.UnitLibrary` and `Curl.Engine.UnitLibrary`, and `Libcurl`'s option setters for 1605; find each with the Glossary and `rg`. 3214 checks the byte sizes of C structs (`sizeof`), for which Curl has no counterpart: record that reason (the one case in this batch whose reason is lasting). Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count.

## Acceptance criteria

- [ ] Each of the cases 1303, 1323, 1399, 1605, 1606 and 3216 has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so. Case 3214 has the Notes entry and the ADR row.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches` in the same commit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/Limits/ and their tests in Curl.Conformance.UnitProgram.UnitTests/Limits/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Limits.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 84511890 2e3e75ab e66ae9dd c0723528 620ebead.
