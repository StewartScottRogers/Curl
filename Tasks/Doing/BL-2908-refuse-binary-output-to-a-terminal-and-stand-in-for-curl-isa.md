---
id: BL-2908
title: Refuse binary output to a terminal and stand in for CURL_ISATTY so upstream cases 1425 and 1426 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/StandardOutputOpener.cs, Curl.Console/TerminalBinaryOutputRefusingStream.cs, Curl.Console.UnitTests/TerminalBinaryOutputRefusingStreamTests.cs, Curl.Console.UnitTests/CurlCommandRunnerTerminalBinaryOutputTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.ToolIsAttyStandIn.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed:
---
# BL-2908 — Refuse binary output to a terminal and stand in for CURL_ISATTY so upstream cases 1425 and 1426 run

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

- 2026-10-21 (lane 7, first claim, no code written): measured `$env:CURL_UPSTREAM_CASE='1425'`: "test1425 is skipped: Curl lacks the feature Debug". Prerequisite pass:
  - `touches` widened. `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` is needed because `RunCurlAsync` (the only caller that composes curl for a case) must pass the new `standardOutputIsTerminal`; the dialing `CreateRunner` overload cannot read `CURL_ISATTY` itself, because `InProcessCurl` (Curl.Api's path) also uses it and a release build must never honour that debug variable. `UpstreamConformanceTests.DebugStandIns.cs` takes the one `DebugStandInRules` line the Context asks for. The refusal is planned as its own stream, `Curl.Console/TerminalBinaryOutputRefusingStream.cs`, wrapped around standard output only when no `-o`/`-O` names a file and `--output -` was not given, with its tests in the two named `Curl.Console.UnitTests` files.
  - Requeued because BL-2887 (in Doing) also touches `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`. No other prerequisite found: `CurlCommandRunner` already takes `standardOutputIsTerminal` (progress meter, styled output) and `Program.cs` already passes it, so only the dialing overload needs the parameter.
  - Upstream rule to implement (`src/tool_cb_wrt.c`, `tool_write_cb`, curl-8_22_0): when standard output is a terminal (debug build: `CURL_ISATTY` set), fewer than 2000 body bytes were written so far, `--output -` was not given, and the chunk holds a NUL byte, warn "Binary output can mess up your terminal. Use "--output -" to tell curl to output it to your terminal anyway, or consider "--output <FILE>" to save to a file." (wrapped as every `Warning:`), set the synthetic error so no `curl: (23)` line follows, and fail the write: exit 23. Real curl's release build cannot be made to see a terminal through `Record-CurlExchange.ps1` (it redirects stdout), so pin the text from that source and say so in the test.
  - Open question for the next claim, check first: test1425's `<stdout>` expects the response headers although its command has no `-i`; find in curl-8_22_0's `src/tool_cb_hdr.c`/`tool_operate.c` what prints them (terminal-only behaviour under `CURL_ISATTY`?) before coding, since 1426's `<stdout>` shows them too.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: c652770a 999fa8c2 8a66be86 e5c59a3b.
- 2026-10-21: Doing -> Backlog. Needs Curl.Conformance.UnitTests/UpstreamConformanceTests.cs (RunCurlAsync), which BL-2887 in Doing also touches
- 2026-10-21: Backlog -> Doing. Criteria at claim: c652770a 999fa8c2 8a66be86 e5c59a3b.
