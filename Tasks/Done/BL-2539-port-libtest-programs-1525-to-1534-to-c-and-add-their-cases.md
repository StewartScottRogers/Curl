---
id: BL-2539
title: Port libtest programs 1525 to 1534 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2538]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2539 — Port libtest programs 1525 to 1534 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1525 to 1534 (batch 13) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 13 of 26: programs 1525, 1526, 1527, 1528, 1529, 1530, 1531, 1532, 1533, 1534. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1525, lib1526, lib1527, lib1528, lib1529, lib1530, lib1531, lib1532, lib1533, lib1534 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten exist as C, each run only by its own case (`lib%TESTNUMBER`). Every Libcurl call they need existed (`CURLOPT_PROXYHEADER`, `CURLOPT_HEADEROPT`, `CURLOPT_OPENSOCKETFUNCTION`, `CURLOPT_KEEP_SENDING_ON_ERROR`, `curl_easy_pause`, `curl_easy_duphandle`, `curl_easy_reset`, `CURLINFO_FILETIME`, `CURLINFO_NUM_CONNECTS`).
- Ports: `LibProxyTunnelUpload` (lib1525, lib1526, lib1527 differ only in their header lists and `CURLOPT_HEADEROPT`, so one class with three instances, as `LibOneVerboseTransfer` does), `Lib1528` to `Lib1534`; tests in `Lib1525To1534Tests.cs`.
- Choices (sensible defaults, no ADR needed): `curl_slist_append`, `curl_easy_getinfo` and `curl_easy_duphandle` failure checks are left out, as earlier ports do (Libcurl's do not fail on a live handle). lib1529's 512-byte `snprintf` buffer is not modelled: a case URL never nears it. lib1531's unchecked `curl_easy_init`/`curl_multi_init` run through `EasyInit`/`MultiInit` (differs only with no handle, where upstream would crash); its message loop reads one message, `CURLMSG_DONE` being the only type.
- On the ratchet: 1528, 1529, 1530, 1531. Platform check: 1528 and 1531 compare only protocol bytes, 1529 only exit 3 and 1530 only exit 7; none prints a path, OS error text or certificate.
- Not on it, filed (engine gaps, not port faults): 1532 (exit 22, `curl_easy_reset failed to zero the response code`) and 1534 (exit 2, `filetime init failed; expected -1 but is 30`): `ManagedEasyTransfer.Reset()` keeps the last `TransferInfo` - BL-2644. 1525, 1526, 1527 (`curl did not finish within 20 seconds`, tunnelled PUT with `CURLOPT_INFILESIZE`) - BL-2645. 1533 (`curl did not finish within 20 seconds`, read-callback pause resumed from the write callback) - BL-2646.
- Quality: one `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` run reported 0 failing members. Full build clean (0 warnings); fast tests green in every test project (LibTest 615, Conformance 3245 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 066d5582 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1525 to lib1534 ported to C# over Libcurl; cases 1528 to 1531 on the ratchet; 1525-1527, 1532-1534 filed as BL-2644 to BL-2646
