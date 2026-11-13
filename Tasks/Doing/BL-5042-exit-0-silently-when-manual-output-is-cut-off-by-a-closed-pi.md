---
id: BL-5042
title: Exit 0 silently when --manual output is cut off by a closed pipe
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerManualClosedPipeTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5042 — Exit 0 silently when --manual output is cut off by a closed pipe

## Goal

`curl --manual` into a pipe that closes early stops quietly with exit 0 and no stderr, as REF does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-047 (Low, Confirmed).

- **Repro:** `curl --manual | head -1; echo "rc=${PIPESTATUS[0]}"`
- **Expected (REF and REF2):** the first manual line, no stderr, `rc=0`.
- **Actual (PORT):** the first manual line, then `curl: (2) The pipe has been ended.`, `rc=2`.
- **Where to start:** `Curl.Console/CurlCommandRunner.cs` lines 1189-1193: the `IOException` from `WriteStandardOutputLinesAsync(informationLines)` reaches the top-level handler. Catch it there and return `CurlExitCode.Ok` without a message.

## Acceptance criteria

- [ ] A new test file `Curl.Console.UnitTests/CurlCommandRunnerManualClosedPipeTests.cs` runs `--manual` with a standard output whose write throws `IOException` and asserts exit 0 and empty stderr.
- [ ] Regression: `--manual` with a working stdout still prints the whole manual and exits 0; an `IOException` from a different output (for example a transfer body write) is not swallowed by this change.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Check whether `--help`, `--ai-help` and `-V` closing early behave the same in REF before widening the catch; keep to `--manual` unless REF agrees.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 0285f0cd f4061204 a0b49a29.
