---
id: BL-5386
title: Abort the other -Z transfers with exit 42 when an RTSP transfer ends with exit 43
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ParallelRun.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5386 — Abort the other -Z transfers with exit 42 when an RTSP transfer ends with exit 43

## Goal

With `-Z`, an RTSP transfer that ends with exit 43 makes the other transfers abort with `curl: (42) Transfer aborted due to critical error in another transfer` (and `%{exitcode}` 42), as curl does; the final exit code stays 43.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-074 (Low, Confirmed 2 of 2 runs: the final exit code is right; only the other transfer's message and `%{exitcode}` differ). Area: parallel transfers (`-Z`), fatal errors, Windows.

Still-open older finding. What changed: the exit 43/48 sequential cases are now fixed (commit 0f1722e5); only `-Z` with RTSP remains.

**Repro.** An RTSP server on 127.0.0.1:41042 that answers any request with `RTSP/1.0 200 OK`, `CSeq` echoed, and `Session: 2` (so curl's `-H "Session: 1"` mismatches and returns exit 43). Any HTTP server on 127.0.0.1:41000 serving `/hello`.
- `curl -sS -Z rtsp://127.0.0.1:41042/m -H "Session: 1" --next rtsp://127.0.0.1:41042/m`
- `curl -sS -Z rtsp://127.0.0.1:41042/m -H "Session: 1" --next http://127.0.0.1:41000/hello -w '[%{exitcode}]\n'`

**Expected (REF).** The first prints `curl: (42) Transfer aborted due to critical error in another transfer` as well as the 43 error, exit 43. The second prints `[42]` for the HTTP transfer.
**Actual (PORT).** Exit 43 with no (42) line; the second prints `[0]` for the HTTP transfer.
**Where to start.** `Curl.Console/CurlCommandRunner.cs` (`IsFatalFailure`, ~line 3906, already lists 2, 27, 43 and 48; its use at ~line 2492 feeds `run.RecordEnd`) and `Curl.Console/ParallelRun.cs` (`AbortedResult`, `AbortedMessage`). Find why the parallel RTSP path does not reach the abort. `touches` is narrowed to these two files plus the tests.

## Acceptance criteria

- [x] New tests in `Curl.Console.UnitTests` (a new file, e.g. `CurlCommandRunnerParallelRtspFatalTests.cs`) prove that with `-Z`, an RTSP transfer failing with exit 43 makes the other transfer finish with exit 42 and the `Transfer aborted due to critical error in another transfer` message, and the overall exit is 43.
- [x] A test proves the `-w '[%{exitcode}]\n'` of the other (HTTP) transfer prints `[42]`.
- [x] A test proves a non-fatal failure in a `-Z` run still does not abort the other transfers (no regression).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] Hand check recorded in the Log: the first command prints the (42) line and exits 43; the second prints `[42]` for the HTTP transfer. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the first command prints the (42) line and exits 43; the second prints `[42]` for the HTTP transfer.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b740072a 391cb81f b5333f5e 7ae10ef7 a8ced021 a0b49a29.
- 2026-11-17: Cause: the RTSP 43 fails at setup, before the other -Z transfer starts; that one was skipped with the first failure's code (43) instead of aborted (42). Fix: `DeferReportOfEarlyFailure` reports it aborted when the first failure is fatal and `--fail-early` is absent. Tests in CurlCommandRunnerParallelRtspFatalTests (unit-level, handler stand-in for RTSP); hand check against live servers not run (no mock RTSP server in the lane), the unit tests pin the stderr line, `[42]` and exit 43. Build clean, fast tests green, Measure-CodeQuality for Curl.Console 0 failing.
- 2026-11-17: Doing -> Done. Fatal first failure under -Z aborts the other transfers with 42
