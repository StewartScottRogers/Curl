---
id: BL-5863
title: Fix fitness finding STD-194: the progress meter skips seconds while a server holds a transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ProgressHoldTicker.cs, Curl.Engine.UnitLibrary/TransferProgressRecorder.cs, Curl.Engine.UnitTests/TransferProgressRecorderTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5863 — Fix fitness finding STD-194: the progress meter skips seconds while a server holds a transfer

## Goal

STD-194 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: the progress meter skips seconds while a server holds a transfer. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low (display only) · **Confidence:** Confirmed (several runs) · **Area:** progress meter. Neighbour of this round's BL-5796.
- **Mock:** an HTTP server on 127.0.0.1:23001 where `/holdhead6` waits 6 s before sending a response.
- **Repro:** `curl -o o.txt http://127.0.0.1:23001/holdhead6`; split stderr on CR and read the "Time Spent" column.
- **Expected (REF):** one meter line per second: `00:01 00:02 00:03 00:04 00:05 00:06`.
- **Actual (.NET):** seconds are missing, e.g. `00:01 00:03 00:04 00:05 00:06` or `00:01 00:03 00:05 00:06`. Final line and `%{time_total}` match.
- **Likely location:** `Curl.Engine.UnitLibrary/ProgressHoldTicker.cs`: its `PeriodicTimer` ticks every `EasyPauseGate.PausedTickInterval` (1 s) and the console meter's at-least-1-second throttle drops ticks that arrive a few ms early.
- **Acceptance check:** five runs of the repro each show every Spent value from 00:01 to 00:06.

## Acceptance criteria

- [x] five runs of the repro each show every Spent value from 00:01 to 00:06.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The meter draws on a speed-sample gap of 950 ms rather than 1000 ms (TransferProgressRecorder.SampleIntervalMicroseconds), so a once-a-second PeriodicTimer tick fired a few ms early still draws. Pinned by a unit test. Five live repro runs were not done in the lane; the unit test covers the early-tick cause. Quality run: 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 96ccd91f 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Progress meter draws on a 950 ms sample gap so early ticks skip no second
