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
completed:
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

- [ ] New tests in `Curl.Console.UnitTests` (a new file, e.g. `CurlCommandRunnerParallelRtspFatalTests.cs`) prove that with `-Z`, an RTSP transfer failing with exit 43 makes the other transfer finish with exit 42 and the `Transfer aborted due to critical error in another transfer` message, and the overall exit is 43.
- [ ] A test proves the `-w '[%{exitcode}]\n'` of the other (HTTP) transfer prints `[42]`.
- [ ] A test proves a non-fatal failure in a `-Z` run still does not abort the other transfers (no regression).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log: the first command prints the (42) line and exits 43; the second prints `[42]` for the HTTP transfer. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the first command prints the (42) line and exits 43; the second prints `[42]` for the HTTP transfer.

## Log

- 2026-11-17: Created.
