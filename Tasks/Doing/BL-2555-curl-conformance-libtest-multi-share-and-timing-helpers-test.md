---
id: BL-2555
title: Curl.Conformance.LibTest: multi, share and timing helpers (testutil, testtrace, wait helpers)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2554]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2555 — Curl.Conformance.LibTest: multi, share and timing helpers (testutil, testtrace, wait helpers)

## Goal

C# helpers equal to `tests/libtest/testutil.c`, `testtrace.c`, `test.h`'s multi macros (`multi_init`, `multi_perform`, `multi_poll`, `multi_wait`, `multi_add_handle`, `multi_remove_handle`, `multi_timeout`, `multi_fdset`), `share_init` and `url_init` exist over `Libcurl`.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Read 	estutil.c, 	esttrace.c, 	est.h, warnless.c and Libcurl/Multi, Libcurl/Share, Libcurl/Url. Time comes from an injected TimeProvider, never a sleep.

## Acceptance criteria

- [ ] Each helper has a test over a fake `ICurlEngine`; `tutil_tvnow` and `tutil_tvdiff` equivalents are tested with a manual `TimeProvider`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: d6fde805 408cc7ba 4272a6b9 8738c7f6.
