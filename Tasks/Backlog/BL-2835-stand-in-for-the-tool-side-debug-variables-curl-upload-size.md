---
id: BL-2835
title: Stand in for the tool-side debug variables CURL_UPLOAD_SIZE, CURL_TIME, CURL_DEBUG_SIZE, CURL_VERSION, CURL_ISATTY, CURL_DBG_SUPPRESS_CONNECT_HDS and the form-boundary entropy so upstream cases 447, 970, 972, 1425, 1426, 1981, 2349, 2300 and 1662 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.ToolDebugVariablesStandIn.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Console/StandardOutputOpener.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2835 — Stand in for the tool-side debug variables CURL_UPLOAD_SIZE, CURL_TIME, CURL_DEBUG_SIZE, CURL_VERSION, CURL_ISATTY, CURL_DBG_SUPPRESS_CONNECT_HDS and the form-boundary entropy so upstream cases 447, 970, 972, 1425, 1426, 1981, 2349, 2300 and 1662 run

## Goal

Upstream cases 447, 970, 972, 1425, 1426, 1981, 2349, 2300 and 1662, which need a debug-build variable of the curl tool or the form boundary, run in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or have a recorded reason.

## Context

Gap scorecard 2026-10-19_1329: eight are `excluded | debug-build-only`, 1662 `libcurl-api`; on Windows they are skipped with `Curl lacks the feature Debug` (2300 runs and differs at the `Sec-WebSocket-Key`). One stand-in per variable, each as ADR-0687/0678/0698 do it: `UpstreamConformanceTests.PlatformFor` reports `Debug` to a case that sets the variable and the harness hands the tool or engine the stand-in. Read upstream's source at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0) for each variable's exact effect before writing it:

- 447 (HTTP PUT with growing file) sets `CURL_UPLOAD_SIZE=498`: the upload's size the debug build reports.
- 970 and 972 (`--write-out %{json}`, JSON output) set `CURL_TIME=13`, `CURL_DEBUG_SIZE=4019` and `CURL_VERSION=curl-unit-test-fake-version`: a fixed clock, a fixed size for a debug value and a fixed version string in the output. `TimeProvider` is already injected (root CLAUDE.md); the version string belongs to the curl tool's `curl_version` in `-w` output.
- 1981 (`%time` in `--write-out`) sets `CURL_TIME=1754037103` and `LC_TIME=C`.
- 1425 and 1426 (refuse and allow binary output to a terminal) set `CURL_ISATTY=yes`: the tool treats standard output as a terminal. `Curl.Console/StandardOutputOpener.cs` is where the tool asks.
- 2349 (HTTP CONNECT `-D` file) sets `CURL_DBG_SUPPRESS_CONNECT_HDS=1`: the CONNECT response headers are not written to the dump file.
- 2300 (WebSockets upgrade only through the curl tool) sets `CURL_ENTROPY=12345678` with `ws`: ADR-0678 stood in for it only for the libtest engine (`ForcedEntropyWebSocketRandomSource`); give the curl tool the same source.
- 1662 (`lib1662`, HTTP formpost from callback and a redirect) names `Mime,Debug`: the form boundary comes from the forced entropy (`Curl.Conformance.UnitTests/ForcedEntropyFormBoundary.cs` exists); report `Debug` to it and give the libtest engine that boundary source.

All stand-ins belong to the harness: neither `Libcurl` nor the shipped curl tool reads these variables. If a case still differs after its stand-in, record the first difference in Notes and file the fix as a new task. If nine cases prove more than one run, finish the first five in the order listed and file the rest as a new task.

## Acceptance criteria

- [ ] `PlatformFor` (or the libtest engine setup for 1662) reports `Debug` to a case that sets one of the variables above and to no case that sets none; unit tests pin the variable list.
- [ ] Each stand-in has a unit test that fails without it: fixed time and version in the `%{json}` output (970, 972), `%time` with `LC_TIME=C` (1981), the terminal answer (1425, 1426), the suppressed CONNECT headers (2349), the forced WebSocket key in the tool (2300), the forced form boundary (1662), the upload size (447).
- [ ] Each of the nine cases ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`, or has its first difference in Notes and a follow-up task.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
