---
id: BL-5108
title: Exit 23 when --trace-ascii to stdout meets a closed pipe
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/StandardOutputFailureDeferringStream.cs, Curl.Console/TransferEventOutput.cs, Curl.Console.UnitTests/TraceToClosedStandardOutputExitCodeTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5108 — Exit 23 when --trace-ascii to stdout meets a closed pipe

## Goal

When `--trace-ascii -` writes to stdout and the reader has gone (a closed pipe), curl exits 23 with `curl: (23) Failure writing output to destination`, not exit 2.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-061 (Low, Confirmed: 3 runs Linux, 1 Windows). Area: output / broken pipe, Windows and Linux.

**Repro.** With a server whose `/big` returns 2 MB: `curl -sS --trace-ascii - http://127.0.0.1:25080/big 2>e | head -c1 >/dev/null; echo ${PIPESTATUS[0]}; cat e` (on Windows a 2 MB `file://` source works too).

**Expected (upstream curl, also 8.22 on Windows).** `23` and `curl: (23) Failure writing output to destination, passed 16384 returned ...`

**Actual.** `2` and `curl: (2) Standard output is closed or its reader has gone.` A plain body piped to `head` without tracing exits 23 like upstream.

**Where to start.** `Curl.Console/StandardOutputFailureDeferringStream.cs` throws `OutputWriteFailedException`; when the trace stream is stdout this is mapped to exit 2 instead of `CURLE_WRITE_ERROR` (23).

## Acceptance criteria

- [x] New tests in `Curl.Console.UnitTests/TraceToClosedStandardOutputExitCodeTests.cs` prove that a trace write failing on a stdout stand-in whose reader has gone ends the transfer with `CurlExitCode` 23 (WriteError) and a message starting `Failure writing output to destination`.
- [x] A regression test proves a body write failing on the same stand-in still exits 23, and that a genuinely closed stdout at startup (not a broken pipe) keeps its present exit code.
- [x] Tests are platform-neutral (stand-in streams, no real pipe).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the repro prints `23` and a `curl: (23) Failure writing output to destination` line.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: e4d6b6df a37055d2 04f2c6a6 a0b49a29.
- 2026-11-13: Fixed in Curl.Console/TransferEventOutput.cs (added to touches): a `--trace`/`--trace-ascii -` dump to stdout is written through WriteFailureIgnoringStream, as curl ignores trace write failures, so the body write fails with exit 23. Cause was the trace write throwing OutputWriteFailedException out of the handler's event call. The startup-closed-stdout case is untouched (ClosedStandardOutputStreamTests still pass). Measure-CodeQuality for Curl.Console: 0 failing members. Linux/macOS: tests use stand-in streams, platform-neutral.
- 2026-11-13: Doing -> Done. Trace dump to stdout ignores write failures, so a closed pipe exits 23
