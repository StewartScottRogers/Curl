---
id: BL-2595
title: Port libtest program lib530 (multi socket and timer callbacks) to C# and add its case to the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2595 — Port libtest program lib530 (multi socket and timer callbacks) to C# and add its case to the ratchet

## Goal

lib530 is ported to C# as `Lib530`, registered in `LibTestPrograms.Ported`, and test530 is on the ratchet if it passes.

## Context

Split from BL-2529 (batch 3 of the libtest ports; phase 5 of `Documentation/Planning/Curl-Api-And-libcurl.md`, ADR-0506). `lib530.c` (curl 8.22.0 `tests/libtest`) runs one transfer five times through `curl_multi_socket_action`, with `CURLMOPT_SOCKETFUNCTION` and `CURLMOPT_TIMERFUNCTION` callbacks that fail on a chosen call (`testone(URL, 0,0)`, `(1,0)`, `(2,0)`, `(0,2)`, `(0,3)`), tags its stderr lines `[T530-<max socket>-<max timer>] [<socket calls>/<timer calls>]`, keeps its own read and write socket sets, `select()`s them and calls `socket_action` on ready ones; it uses `libtest_debug_cb` (`LibTestDebugTrace`). Libcurl has `CurlMulti.SocketActionAsync`, `SetSocketFunction` and `SetTimerFunction` (ADR-0588); the test project's `FakeMultiTransfer.SocketActionAsync` throws `NotSupportedException` today and needs a scripted socket-action answer. Only case test530 runs it. A `select()` over the program's own socket set has no BCL equivalent for a `CurlSocket`; decide how to wait (as ADR-0662 did for `select_test`) and record it in an ADR.

## Acceptance criteria

- [ ] `Lib530` is a C# `ILibTest` in `Curl.Conformance.LibTest.UnitLibrary` registered in `LibTestPrograms.Ported`, with focused unit tests in `Curl.Conformance.LibTest.UnitTests` over a fake `ICurlEngine` covering each of the five `testone` runs.
- [ ] test530 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` if it passes through the runner on Windows and its output does not depend on the platform; otherwise it is left off and filed as a task with the board script (its differing lines).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
