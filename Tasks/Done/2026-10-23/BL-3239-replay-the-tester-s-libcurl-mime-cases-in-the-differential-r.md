---
id: BL-3239
title: Replay the tester's Libcurl MIME cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3237]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Mime, Curl.Conformance.Differential.UnitTests/Libcurl/Mime, Curl.Conformance.Differential.IntegrationTests/LibcurlMime]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3239 — Replay the tester's Libcurl MIME cases in the differential runner

## Goal

The differential runner replays the tester's Libcurl MIME cases (`mimefile.json`) and printf inputs (`pf_in.json`) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Drivers: `LibTest/MimeMode.cs`, `LibTest/PrintfMode.cs`; outputs `mimefile.txt`, `pf_out.txt`, `pf_report.txt`.

## Acceptance criteria

- [x] The MIME and printf inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- Plan: MIME cases rebuilt in C# from mimetest.py (LibcurlMimeCases, 64 cases in its order and call order; its skipped "refused" entry left out), so no Cases/ JSON is needed; LibcurlMimeComparer blanks the multipart boundary as mimetest.py nw() did and reports codes and the first wire difference in its shape; drivers in IntegrationTests/LibcurlMime load the reference libcurl with NativeLibrary as ADR-0792 decided (no new ADR: same decision). mimefile.txt and mimefile.json are written to the run's temporary directory.
- Decided: pf_in.json (printf) is left to BL-3303, which already names it with t1.json and Libcurl/Printf in its touches (AF-0148: leave work a Backlog task covers to that task). The first criterion is ticked for the MIME inputs; its printf half is replayed and reported by BL-3303, not here.
- Run 2026-10-23 against `C:\Program Files\Git\ucrt64\bin\libcurl-4.dll` (8.22.0), 2 s: `cases 64 identical 63`. The one difference, sub-encoder, is noise: the base64-encoded inner multipart boundary is random in each library (same length 449, differs only inside the encoded boundary), so nothing is filed.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members. Fast tests: Curl.Conformance.Differential.UnitTests 501 passed. New unit tests are pure in-memory (no paths, OS text or certificates), so platform-neutral for Linux and macOS CI; the integration test uses Path.Combine on a temp directory only.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f9ace538 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner replays the tester's 64 Libcurl MIME cases against Curl's Libcurl and libcurl 8.22.0: 63 identical, the other only the random encoded boundary; printf inputs stay with BL-3303
