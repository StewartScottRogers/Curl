---
id: BL-2889
title: Refuse binary output to a terminal and stand in for CURL_ISATTY so upstream cases 1425 and 1426 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/StandardOutputOpener.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.ToolIsAttyStandIn.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2889 — Refuse binary output to a terminal and stand in for CURL_ISATTY so upstream cases 1425 and 1426 run

## Goal

Curl refuses binary output to a terminal as curl 8.22.0 does, and upstream cases 1425 and 1426 (which set `CURL_ISATTY=yes`) pass in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2835. Curl has no binary-output-to-terminal refusal at all (no `Binary output can mess up your terminal` text anywhere), so a terminal stand-in alone cannot pass 1425. Upstream: `src/tool_cb_wrt.c` at tag `curl-8_22_0` (the `isatty` check and its warning, and the debug build's `CURL_ISATTY`). The harness's dialing `CurlComposition.CreateRunner` overload takes no `standardOutputIsTerminal` today; add one (default `false`) and have `UpstreamConformanceTests.RunCurlAsync` pass it when the case's `<setenv>` sets `CURL_ISATTY`. Report `Debug` to such a case with one rule in `DebugStandInRules` (`UpstreamConformanceTests.DebugStandIns.cs`), its predicate in its own partial file. Measure real curl's refusal text and exit code with `Record-CurlExchange.ps1` before pinning it.

## Acceptance criteria

- [ ] A unit test in `Curl.Console.UnitTests` pins the refusal's warning lines and exit code for binary body bytes written to a terminal standard output, and that `--output -` writes them.
- [ ] `PlatformFor` reports `Debug` to a case setting `CURL_ISATTY` and to no other because of it; a unit test pins it.
- [ ] Cases 1425 and 1426 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, `Curl.Console` keeps 100% line and branch coverage, and `--ai-help` needs no change (no option changes).

## Notes

## Log

- 2026-10-21: Created.
