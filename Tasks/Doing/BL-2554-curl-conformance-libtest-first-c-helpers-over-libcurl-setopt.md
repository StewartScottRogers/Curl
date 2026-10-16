---
id: BL-2554
title: Curl.Conformance.LibTest: first.c helpers over Libcurl (setopt, init and the TEST_ERR codes)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2553]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2554 — Curl.Conformance.LibTest: first.c helpers over Libcurl (setopt, init and the TEST_ERR codes)

## Goal

C# helpers equal to `tests/libtest/first.c` and `test.h` exist: `test_setopt`, `easy_init`, `global_init`, `easy_perform`, `easy_getinfo`, `curl_slist_append` checks, the `TEST_ERR_*` codes with `res`-propagating semantics, and `libtest_arg2` / `libtest_arg3` / `URL` access.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Read 	ests/libtest/first.c, irst.h and 	est.h in the tarball and Libcurl as it is (Libcurl/Easy, Libcurl/Global, Libcurl/Options). Exit code values must equal upstream's TEST_ERR_* numbers.

## Acceptance criteria

- [ ] Each helper has a test that drives it over a fake `ICurlEngine` and pins the `TEST_ERR_*` code it returns on each failing step.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: e5cb0805 408cc7ba 4272a6b9 8738c7f6.
