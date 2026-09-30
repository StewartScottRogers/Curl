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
completed:
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

- [ ] A run with the default response and `-CurlArgs '-s','http://127.0.0.1:<port>/'` writes `timing.json` beside the other four files; it parses with `ConvertFrom-Json` and has the four fields, `elapsedMilliseconds` > 0.
- [ ] The same run with `-ResponseDelayMilliseconds 500` reports `elapsedMilliseconds` of at least 500 and `samples` greater than 10.
- [ ] `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` are byte-identical to a run of the script before this change for the same arguments (compare with `git stash` or a copy of the old script).
- [ ] The comment-based help's `.DESCRIPTION` lists `timing.json` with the other files, and states the sampling limitation.
- [ ] The script still runs under Windows PowerShell 5.1 and contains only ASCII characters.

## Notes

## Log

- 2026-09-29: Created.
