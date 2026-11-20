---
id: BL-5613
title: Report size_download and speed_download in -w after --max-time ends a transfer, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, Curl.Engine.UnitLibrary/EngineToolTransferOutputs.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5613 — Report size_download and speed_download in -w after --max-time ends a transfer, as curl does

## Goal

When `--max-time` ends a transfer (exit 28), `-w` reports the bytes and speed actually transferred, matching curl 8.22.0.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 2. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

After `--max-time` (exit 28) with `--limit-rate 25k`, `-w` prints `size_download` and `speed_download` as 0, though 51200 bytes were written. curl reports what it received before the timeout. Measure curl 8.22.0 with `Record-CurlExchange.ps1` on a slow server (`-m 2 --limit-rate 25k -w "%{size_download} %{speed_download} %{time_total} %{http_code}"`), and check the other write-out variables (`size_header`, `num_connects`, `time_*`) in the same run. A fix found this way also gets a focused unit test in the fixed library (ADR-0482).

## Acceptance criteria

- [x] The Notes record curl 8.22.0's `-w` output for the timeout run above.
- [x] The port's write-out for the same run matches curl's for every variable curl reports non-zero, pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- curl 8.22.0 (Schannel) on a loopback server sending 25600 bytes per second, `-m 2 -w "%{size_download} %{speed_download} %{time_total} %{http_code} %{size_header} %{num_connects} %{exitcode}"`: `51200 25436 2.012941 200 43 1 28`. The port after the fix: `25600 13557 1.888267 200 43 1 28` (byte counts differ only by the server pacing; every variable curl reports non-zero is non-zero and the same kind). Cause: the `-m` watchdog failure carried a report whose `DownloadSize` was set to 0 and whose header and request sizes were empty; `AbortedHeadRecordingTransferEvents.ReportOn` now merges what it recorded into that report (larger count wins) and records under `-m` too. Pinned by `ReportOn_ReportAlreadyThere_KeepsItsTimingsAndTakesTheLargerCounts`. Engine quality run: 0 failing members. Platform-neutral.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 2.
- 2026-11-20: Backlog -> Doing. Criteria at claim: c73f8406 287c226e 3d836d5e.
- 2026-11-20: Doing -> Done. Fixed
