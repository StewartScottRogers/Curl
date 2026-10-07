---
id: BL-521
title: Draw curl's parallel progress meter during a -Z run
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-521 — Draw curl's parallel progress meter during a -Z run

## Goal

A `-Z` run without `-s` writes curl 8.21.0's combined parallel progress meter to standard error (its own header and columns for transfers, live transfers, totals and speed), not one single-transfer meter per URL.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker).
- The single-transfer meter: `Curl.Console/ProgressMeterLines.cs`, `ProgressMeterFields.cs`, `TransferProgressRecorder.cs`; ADR-0082, ADR-0092 and ADR-0099 describe how it is drawn from the handlers' byte reports.
- The parallel meter's header and column layout must be measured, not recalled: run the reference curl with `-Z` against `Record-CurlExchange.ps1 -Connections 2 -ResponseDelayMilliseconds 1500` and capture standard error.

## Acceptance criteria

- [x] Measured first as above (and with `-#`, which curl may ignore in parallel mode); stderr copied into Notes byte for byte.
- [x] Tests on a fake `TimeProvider` pin the header, a mid-run line and the final line for two transfers as measured.
- [x] `-s` and `--no-progress-meter` suppress it, with tests.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-28 on curl 8.21.0 (Schannel, Windows 11) with `Record-CurlExchange.ps1 -Port 18521
-ResponseDelayMilliseconds 1500 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789'`.
The standard error below is byte for byte, with `\r` and `\n` written as escapes. Every run exited 0.

- `-Z A B` (`-Connections 2`):
  `DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed\r\n\r--  --      0     0     2     1                                 0      \r--  --      0     0     2     1           00:00:01              0      \r--  --     10     0     2     1           00:00:01              6      \r--  --     10     0     2     1           00:00:02              3      \r100 --     20     0     2     0  00:00:03 00:00:03              6     \r\n`
- `-Z --parallel-immediate A B`:
  `DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed\r\n\r--  --      0     0     2     2                                 0      \r--  --      0     0     2     2           00:00:01              0      \r--  --     10     0     2     1           00:00:01              6      \r--  --     10     0     2     1           00:00:02              3      \r100 --     20     0     2     0  00:00:03 00:00:03              6      \r100 --     20     0     2     0  00:00:03 00:00:03              6     \r\n`
- `-Z -# A B`: `-#` is ignored, and the output is the same as `-Z A B`:
  `DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed\r\n\r--  --      0     0     2     1                                 0      \r--  --      0     0     2     1           00:00:01              0      \r--  --     10     0     2     1           00:00:01              6      \r--  --     10     0     2     1           00:00:02              3      \r100 --     20     0     2     0  00:00:03 00:00:03              6     \r\n`
- `-Z A B C` (`-Connections 3`):
  `DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed\r\n\r--  --      0     0     3     1                                 0      \r--  --      0     0     3     1           00:00:01              0      \r--  --     10     0     3     2           00:00:01              6      \r--  --     10     0     3     2           00:00:02              3      \r--  --     20     0     3     1           00:00:03              6      \r--  --     20     0     3     1           00:00:04              4      \r100 --     30     0     3     0  00:00:05 00:00:04              6      \r100 --     30     0     3     0  00:00:05 00:00:04              6     \r\n`
- `-Z -s A B` and `-Z --no-progress-meter A B`: no bytes.
- Sizes, from `curl -Z -o NUL -o NUL file:///Z:/tmp521big/fN file:///Z:/tmp521big/fN` with N at
  150000, 5000000 and 150000000. The two final lines read `Dled` ` 292k`, `9765k` and ` 286M`, and
  `Speed` `71.5M`, `1589M` and `3043M`. This is `max5data` as in curl's source.

Plan and what was learned:
- The format is `progress_meter` in curl's `src/tool_progress.c`: `%-3s %-3s %s %s %5d %5d  %s %s %s %s %5s`.
  The last `%5s` holds the final line's `\n`, which Windows text mode writes as `\r\n`.
  `time2str` is blank (eight spaces) for zero. `Left` is blank on the final line because zero is
  left.
- The line layout comes from `Curl.Output`'s `ParallelProgressMeterText` (new, public), and the
  figures from `Curl.Console`'s `ParallelProgressMeter` and `ParallelTransferProgress`, which the
  run's `ParallelRun` owns. `TransferProgressRecorder` passes every byte report on, and the runner
  stops drawing per-transfer meters and bars under `-Z`. When to draw is recorded in ADR-0155.
- Default taken: `Xfers` counts the transfers that have taken a `--parallel-max` slot, while curl
  counts the transfers it has created, up to twice `--parallel-max`. The two agree unless more
  transfers are queued than `--parallel-max` (ADR-0155, decision 4).
- Default taken: `-s` and `--no-progress-meter` are read from the first option group, because curl's
  flags are global.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0155 and its index row. None of the
  tasks in Doing (BL-640, BL-703) names it.
- Our binary, run against the same server, gives the same layout and counts. It also drew an extra
  ` 50` line where transfer B's size was known before its body arrived, which curl's algorithm also
  does when the headers arrive first.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. A -Z run draws curl 8.21.0's combined parallel progress meter; -s and --no-progress-meter hide it, -# is ignored
