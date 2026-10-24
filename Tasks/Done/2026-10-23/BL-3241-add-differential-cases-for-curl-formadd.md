---
id: BL-3241
title: Add differential cases for curl_formadd
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3239]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Formadd, Curl.Conformance.Differential.UnitTests/Libcurl/Formadd, Curl.Conformance.Differential.IntegrationTests/LibcurlFormadd]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3241 — Add differential cases for curl_formadd

## Goal

New differential cases exercise `curl_formadd`, `curl_formget` and `curl_formfree` (every CURLFORM_* option, its error codes, and the posted multipart body) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester did not cover curl_formadd. Builds on BL-3239's MIME comparison.

## Acceptance criteria

- [x] formadd cases covering every CURLFORM_* option and each CURL_FORMADD_* error replay against both libraries; one run's report is recorded under Notes, each difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- Plan: the formadd cases are built in C# (`LibcurlFormaddCases`, 49 cases): every CURLFORM_* option 0 to 20 (unknown 99 too) and the CURL_FORMADD_* errors OPTION_TWICE, NULL, UNKNOWN_OPTION, INCOMPLETE and ILLEGAL_ARRAY; MEMORY and DISABLED cannot be reached without a failed allocation or a build without the form API, so no case gives them. Each case logs every `curl_formadd` code, then `curl_formget` (output joined in front of the wire log) and a `CURLOPT_HTTPPOST` post to the loopback `/echo`, then `curl_formfree`. Comparison and report reuse BL-3239's `LibcurlMimeComparer` and `LibcurlMimeReportWriter` (boundary blanked). Drivers in IntegrationTests/LibcurlFormadd load the reference libcurl as ADR-0792 decided, so no new ADR.
- Decided: the reference `curl_formadd` is variadic; it is called through a fixed 26-argument pointer-sized signature ending in CURLFORM_END, which the x64 conventions pass as a variadic call's. The `stream` case only adds (no formget or post): with no read callback both libraries would read the process's standard input.
- Run 2026-10-23 against `C:Program FilesGitucrt64binlibcurl-4.dll` (8.22.0), 1 s: `cases 49 identical 49`. No difference, so nothing is filed.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members. Fast tests: Curl.Conformance.Differential.UnitTests 518 passed; builds clean with -warnaserror. The new unit tests are pure in-memory (no paths, OS text or certificates), so platform-neutral for Linux and macOS CI; the integration test uses Path.Combine on a temporary directory only.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: be35836b ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential runner replays 49 curl_formadd/curl_formget/curl_formfree cases covering every CURLFORM_* option and reachable CURL_FORMADD_* error against Curl's Libcurl and libcurl 8.22.0: all 49 identical
