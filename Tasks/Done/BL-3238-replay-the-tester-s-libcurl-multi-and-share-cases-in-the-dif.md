---
id: BL-3238
title: Replay the tester's Libcurl multi and share cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3237]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Multi, Curl.Conformance.Differential.UnitTests/Libcurl/Multi, Curl.Conformance.Differential.IntegrationTests/LibcurlMulti, Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlMulti]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3238 — Replay the tester's Libcurl multi and share cases in the differential runner

## Goal

The differential runner replays the tester's Libcurl multi and share cases (`m_in.json`, `m2.json`, `m2i.json`, `m3.json`, `mi.json`, `sh.json`) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Drivers: `LibTest/MultiMode.cs`, `LibTest/ShareMode.cs`; outputs `m_out.txt`, `m2o.txt`, `m3o.txt`, `mo.txt`, `sho.txt`.

## Acceptance criteria

- [x] The multi and share inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

- Touches widened: Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlMulti holds the embedded case files (the library embeds Cases/**/*.json); no Doing task on origin/work/dark-factory names it.
- Decided (ADR-0793): one LibcurlMultiReplayer writes the tester's tokens for both libraries through ILibcurlMultiTarget (unit tested with a recording fake and a stepped TimeProvider); a step naming an easy handle the case already cleaned up is written `freed` without a call (m_in case 12), as a freed handle is undefined behaviour in the reference libcurl; info=[...] sorts ordinally. mi.json is a mime input (mo.txt holds mime tokens), so it is left to BL-3239. /r302 has no loopback route, so both libraries get the same default answer for it.
- Run 2026-10-23 against C:/Program Files/Git/ucrt64/bin/libcurl-4.dll (8.22.0), 25 s: `cases 5 identical 4 different 1` (m2, m2i, m3, sh identical; m_in differs in 5 of 36 lines). Cases 1, 2 and 14: only the q numbers in info=[...] (the order concurrent loopback transfers finish), which varies run to run: noise, not filed. Case 24: reference poll=0/1/0 poll=0/1/0 wait=0/1/0, ours poll=0/0/4 x2 and wait=0/0/4 - filed BL-3304. Case 34: after runfor ends in 28, reference response code 200, ours 0 - filed BL-3305.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: first run flagged LibcurlMultiCaseReader.ReadStep (complexity 14), rewritten as a table of readers; second run 0 failing members, 100% line and branch, worst CRAP 10. dotnet build of the Differential projects clean (warnings are errors); Curl.Conformance.Differential.UnitTests 500 passed. New unit tests are in-memory (no paths, OS text or certificates), so platform-neutral for Linux and macOS CI.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 12b4f533 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner replays the tester's Libcurl multi and share cases against Curl's Libcurl and libcurl 8.22.0; differences filed as BL-3304 and BL-3305
