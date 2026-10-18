---
id: BL-2549
title: Port libtest programs 1978 to 2306 to C# and add their cases to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2548]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-16
completed: 2026-10-17
---
# BL-2549 — Port libtest programs 1978 to 2306 to C# and add their cases to the ratchet

## Goal

The libtest programs numbered 1978 to 2306 (batch 23) are ported to C# and every upstream case that runs them and passes is on the ratchet.

## Context

Phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md` and ADR-0506. Upstream curl 8.22.0 ships its libtest programs as C in the tarball's `tests/libtest`; the vendored `Curl.Conformance.UnitTests/UpstreamTestData` holds only the case files. A case runs a program through its `<client><tool>` line (`lib%TESTNUMBER` or `libNNN`); 259 distinct programs serve 364 cases (the plan's "362 programs" counted source files). The ports are C# `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, written over `Libcurl`, with tests in `Curl.Conformance.LibTest.UnitTests`. Batch 23 of 26: programs 1978, 1985, 2023, 2032, 2082, 2118, 2301, 2302, 2304, 2306. Read each program's C source in the tarball, its case file under `UpstreamTestData` and the helpers from the harness tasks; add a helper to the harness library when two ports need it, with its own test. Where a program needs a Libcurl feature that is missing, file an engine or Libcurl task and add it to this task's `depends-on`, moving the task to Backlog, in the first turns.

## Acceptance criteria

- [x] Each of lib1978, lib1985, lib2023, lib2032, lib2082, lib2118, lib2301, lib2302, lib2304, lib2306 (those that exist in tests/libtest; a number whose case uses another tool is noted under Notes) is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in the runner's table, with a focused unit test in `Curl.Conformance.LibTest.UnitTests` that drives it over a fake `ICurlEngine`.
- [x] Every upstream case that runs one of these programs, and passes through the runner on Windows, is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; a case that fails is left off and filed as a task with the board script (its test number and the differing lines), not repaired here beyond the port.
- [x] No added case's output depends on the platform (path separators, drive letters, OS error text, certificates); one that does is pinned per platform or left off.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- 2026-10-17 (lane 1): sources read from curl-8_22_0's `tests/libtest` (raw.githubusercontent.com). All ten programs exist as C. Cases served: lib1978 (1978), lib1985 (1985), lib2023 (2023 to 2031), lib2032 (2032), lib2082 (2082 to 2086), lib2118 (2118), lib2301 (2301), lib2302 (2302, 2303), lib2304 (2304), lib2306 (2306). Ports `Lib1978`, `Lib1985`, `Lib2023`, `Lib2032` (with `Lib2032Connections`), `Lib2082`, `Lib2118`, `Lib2301`, `Lib2302` (with `Lib2302Frames`), `Lib2304` and `Lib2306`; tests in `Lib1978To2306Tests.cs`. The fake engine gained a `WebSocket` (`FakeEasyWebSocket`) so the `curl_ws_*` calls can be driven. `first.c`'s `ws_send_ping`, `ws_recv_pong` and `ws_close` live inside `Lib2304`, the only port in this batch that uses them; move them to a harness helper when a second port needs them.
- Choices (sensible defaults): programs that call `curl_global_init` unchecked run through `WithLibraryAsync`, as earlier batches did. lib2032's `curl_easy_getinfo(CURLINFO_LASTSOCKET)` failure check is left out: Libcurl answers it on a live handle. lib2082's `CURLOPT_WRITEDATA stderr` is a write callback writing the body to the program's stderr (Libcurl's WRITEDATA takes a `Stream`). The programs' own `__LINE__` lines (lib2032's state lines, lib2082's failure) name `lib<N>.c` and upstream's line numbers.
- On the ratchet: 2023, 2082, 2083, 2084, 2085, 2086, 2306. Platform check: none writes a path, drive letter, OS error text or certificate; 2082 to 2086 write addresses and ports the runner substitutes.
- Not on it, filed: 1978 skipped (`Curl lacks the feature Debug`, SigV4 time) - BL-2687 after BL-2675. 2301 to 2304 skipped (`Debug`, `CURL_ENTROPY`) - BL-2688. 2025, 2029 to 2032 (NTLM type-1 `TlRMTVNTUAABAAAAB4IIog...` vs `...ABoIIAAAA...`) - BL-2689 after BL-2635. 1985, 2026 to 2028 (Digest fields joined by `,` with no space) - BL-2690 after BL-2666. 2024 (no Digest Authorization on request 200 after the Basic 401) - BL-2691. 2118 (exit 52, expected 60: VERIFYHOST not checked without VERIFYPEER) - BL-2692.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` first found `Lib2032.LoopAsync` (Cx 14), `Lib2082.TestAsync` (Cx 12) and one uncovered branch; after splitting both and one more test: 100% line, 100% branch, 1475 members, 0 failing, worst CRAP 10. Full build clean (0 warnings); fast tests green in every test project (LibTest 959 passed).

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 94c5e924 ceef976f 6c058a97 408cc7ba 4272a6b9 8738c7f6.
- 2026-10-17: Doing -> Done. lib1978 to lib2306 (ten programs) ported to C# over Libcurl; cases 2023, 2082-2086 and 2306 on the ratchet; the rest filed as BL-2687 to BL-2692
