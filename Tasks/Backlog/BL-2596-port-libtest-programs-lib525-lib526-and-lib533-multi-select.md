---
id: BL-2596
title: Port libtest programs lib525, lib526 and lib533 (multi select loops) to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2596 — Port libtest programs lib525, lib526 and lib533 (multi select loops) to C# and add their cases to the ratchet

## Goal

lib525, lib526 and lib533 are ported to C# as `Lib525`, `Lib526` and `Lib533`, registered in `LibTestPrograms.Ported`, and every case running them that passes is on the ratchet.

## Context

Split from BL-2529 (batch 3 of the libtest ports; phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506). All three drive a multi handle with `multi_perform` and a one-second `select_test` loop (see `LibTestRun.SingleMultiTransferAsync`, `LibTestSteps.PerformUntilDoneAsync`, ADR-0662), with cleanup orders that depend on `testnum`:
- `lib525.c`: active-FTP upload (`CURLOPT_FTPPORT "-"`) of the file `libtest_arg2` names, `CURLOPT_READDATA` the open file and `CURLOPT_INFILESIZE_LARGE` its size; usage error `TEST_ERR_USAGE` without arg2, `TEST_ERR_FOPEN` when it cannot open it; cleanup type PA for 529, PB otherwise. Cases 525, 529, 531, 588.
- `lib526.c`: `NUM_HANDLES` (4) easy handles to the same URL run one after another ("Start at URL 0", "Advancing to URL n" on stderr); 527 cleans each up without removing it, 532 removes, `curl_easy_reset`s and re-adds handle 0; cleanup PB for 526 and 528, UB for 527 and 532. Cases 526, 527, 528, 532.
- `lib533.c`: one handle with `CURLOPT_FAILONERROR`, then removed, reset and re-added for `libtest_arg2`; cleanup UB. Cases 533, 534, 535, 546.
Read each program's C source in the 8.22.0 tarball's `tests/libtest`.

## Acceptance criteria

- [ ] `Lib525`, `Lib526` and `Lib533` are C# `ILibTest`s in `Curl.Conformance.LibTest.UnitLibrary` registered in `LibTestPrograms.Ported`, with focused unit tests in `Curl.Conformance.LibTest.UnitTests` over a fake `ICurlEngine`, covering each `testnum` cleanup branch.
- [ ] Every case among 525, 526, 527, 528, 529, 531, 532, 533, 534, 535, 546 and 588 that passes through the runner on Windows and whose output does not depend on the platform is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each failing one is left off and filed as a task with the board script (its test number and differing lines).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
