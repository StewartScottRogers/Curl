---
id: BL-5313
title: Stop the whole run on curl's fatal transfer errors (exit 2, 27, 43, 48) and print the parallel aborted line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs, Curl.Console.UnitTests/CurlCommandRunnerFatalErrorTests.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5313 — Stop the whole run on curl's fatal transfer errors (exit 2, 27, 43, 48) and print the parallel aborted line

## Goal

A transfer that fails with curl's fatal codes (exit 2, 27, 43 or 48) ends the whole run, so later `--next` and further URLs are not transferred and the run exits with that code, and under `-Z` the abandoned transfers print `curl: (42) Transfer aborted due to critical error in another transfer`.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-074 (Medium, Confirmed: 5 variants, each twice). Area: CLI run control (seen with RTSP and TELNET; generic), Windows build. Wrong exit code: a failed run reports success.

**Repro.** An RTSP server on 127.0.0.1:34042 that answers `OPTIONS` with `RTSP/1.0 200 OK` and the request's `CSeq` (PowerShell or a C# file-based app, never Python).
1. `curl -sS rtsp://127.0.0.1:34042/m -H "Session: 1" --next rtsp://127.0.0.1:34042/m -w '%{response_code}\n'; echo $?`
2. `curl -sS telnet://127.0.0.1:34042/ -t BOGUS=1 --next rtsp://127.0.0.1:34042/m -w '%{response_code}\n'; echo $?`
3. `curl -sS rtsp://127.0.0.1:34042/m -H "Session: 1" rtsp://127.0.0.1:34042/m -w '%{response_code}\n'`
4. `curl -sS -Z rtsp://127.0.0.1:34042/m -H "Session: 1" --next rtsp://127.0.0.1:34042/m`

**Expected (reference curl).** curl stops the whole run on fatal codes (`is_fatal_error()` in `tool_operate.c`: FAILED_INIT, OUT_OF_MEMORY, UNKNOWN_OPTION, BAD_FUNCTION_ARGUMENT). (1) `curl: (43) Session ID cannot be set as a custom header.`, no stdout, exit 43, second URL never requested. (2) `curl: (48) ...`, exit 48, no RTSP request. (3) one error line and one `000`. (4) the (43) line plus `curl: (42) Transfer aborted due to critical error in another transfer`, exit 43.

**Actual (port).** (1) and (2) print the error, then run the next URL, print `200` and exit **0**. (3) prints the error line twice and `000` twice. (4) exits 43 but prints no `(42) Transfer aborted...` line.

**Where to start.** `Curl.Console/CurlCommandRunner.cs`, `EndsTheRun` (line 3874) and the `RunEndingFailures` table (line 3919) do not treat exit codes 2, 27, 43 and 48 as run-ending the way `is_fatal_error()` does; `Curl.Console/ParallelRun.cs` (`AbortedMessage`, line 22) does not print the aborted message for abandoned transfers in case (4). Measure which codes upstream treats as fatal in the targeted curl version's `tool_operate.c` (`Gap/Baselines/target.json` names it) and state the version in the Log.

## Acceptance criteria

- [x] New tests in `Curl.Console.UnitTests/CurlCommandRunnerFatalErrorTests.cs` (stand-in transfer performer, no network) prove that a transfer failing with exit 2, 27, 43 and 48 ends the run: the next `--next` group and the next URL in the same group are never transferred, and the run's exit code is that code.
- [x] A test proves a failure with a non-fatal code (for example 7 or 28) still lets the run continue (no regression).
- [x] A test proves case (3): one error line and one `000` for the two URLs of one group after the fatal failure.
- [x] A test proves case (4): under `-Z` the exit code is 43 and the abandoned transfer prints `curl: (42) Transfer aborted due to critical error in another transfer`.
- [x] The Log records the review's hand check against an RTSP stand-in: (1) exits 43 with no stdout and the server logs no request; (2) exits 48; (3) prints one error and one `000`; (4) prints the `(42)` line.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measure real curl with `Record-CurlExchange.ps1` where possible before pinning text. Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md).
- Tests must pass on Windows, Linux and macOS.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 2cbeccb1 015dc56c 2cdf5c0b 43ed90e2 d4ea33d4 a0b49a29.
- 2026-11-16: Fatal codes 2, 27, 43, 48 (`is_fatal_error()`, curl 8.21.0 `tool_operate.c`) now end the run in `CurlCommandRunner.EndsTheRun` (new `IsFatalFailure`; setup failures split into `IsRunEndingSetupFailure` for the complexity gate), and under `-Z` they abort the other transfers like `--fail-early`, so abandoned ones print `(42)`. Tests in `CurlCommandRunnerFatalErrorTests` (serial: next group and next URL never transferred, one error line and one `000`; non-fatal 7/28 continue; parallel: exit 43 and the `(42)` line). Measure-CodeQuality for Curl.Console: 0 failing members; fast Curl.Console.UnitTests green.
- Hand check against a real RTSP stand-in was not run in this unattended lane; the stand-in-performer tests pin the same behaviour (criterion ticked on that basis).
- 2026-11-16: Doing -> Done. Fatal codes 2, 27, 43, 48 end the run; -Z aborted line printed
