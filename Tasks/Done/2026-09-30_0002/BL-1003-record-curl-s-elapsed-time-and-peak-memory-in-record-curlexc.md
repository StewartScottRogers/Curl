---
id: BL-1003
title: Record curl's elapsed time and peak memory in Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-992, BL-996, BL-997, BL-998]
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1003 — Record curl's elapsed time and peak memory in Record-CurlExchange.ps1

## Goal

Every `Record-CurlExchange.ps1` run also writes `timing.json` to its output directory - the executable run, its wall-clock milliseconds and its peak working set in bytes - so the same canned exchange can be timed for real curl and for Curl.

## Context

Lane-eligible. The audit office's performance comparison (BL-1007) runs the same
transfers through real curl and through Curl.Console's native AOT build. CLAUDE.md says
to measure with `Record-CurlExchange.ps1` and extend it rather than write another
server, and it already runs any executable through `-Curl`.

Today the script starts the process around line 2450 with a `Stopwatch` (`$curlClock`)
and prints `curl exited <code> after <n> ms` to the host, but writes no timing file and
measures no memory. `Process.PeakWorkingSet64` cannot be read after the process has
exited on every platform, so sample it: while waiting for exit, every 10 ms call
`Refresh()` and keep the largest `PeakWorkingSet64` read (catching the
`InvalidOperationException` a finished process throws), then take the largest seen.
Replace the bare `WaitForExit()` with a `WaitForExit(10)` loop doing that; the pipes are
already drained asynchronously, so this cannot deadlock.

`timing.json`, UTF-8 without BOM, one object:
`{ "executable": "<full path of -Curl>", "elapsedMilliseconds": <int>, "peakWorkingSetBytes": <long>, "samples": <int> }`.
`peakWorkingSetBytes` is `0` and `samples` is `0` when the process ended before the first
sample; say in the help that very short runs can under-report.

## Acceptance criteria

- [x] A run with the default response and `-CurlArgs '-s','http://127.0.0.1:<port>/'` writes `timing.json` beside the other four files; it parses with `ConvertFrom-Json` and has the four fields, `elapsedMilliseconds` > 0.
- [x] The same run with `-ResponseDelayMilliseconds 500` reports `elapsedMilliseconds` of at least 500 and `samples` greater than 10.
- [x] `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` are byte-identical to a run of the script before this change for the same arguments (compare with `git stash` or a copy of the old script).
- [x] The comment-based help's `.DESCRIPTION` lists `timing.json` with the other files, and states the sampling limitation.
- [x] The script still runs under Windows PowerShell 5.1 and contains only ASCII characters.

## Notes

- Implemented as specified: `WaitForExit(10)` loop samples `PeakWorkingSet64` after `Refresh()`, then a bare `WaitForExit()` so the redirected pipes reach end of stream before `ExitCode` is read. `executable` is `-Curl` resolved to a full path (`Resolve-Path` for a file, `Get-Command` for a bare name, else as given). JSON written with `ConvertTo-Json -Compress`, UTF-8 without BOM, on every mode (including `-NoServer`, whose help now lists it).
- Verified 2026-09-30 on Windows PowerShell 5.1.26100: default run 349 ms, 7 samples, 8.8 MB peak; `-ResponseDelayMilliseconds 500` run 707 ms, 14 samples; request.bin, stdout.bin, stderr.txt and exitcode.txt byte-identical to the pre-change script for the same port and response; script is pure ASCII. dotnet build clean, fast tests green.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Record-CurlExchange.ps1 writes timing.json with elapsed ms and sampled peak working set
