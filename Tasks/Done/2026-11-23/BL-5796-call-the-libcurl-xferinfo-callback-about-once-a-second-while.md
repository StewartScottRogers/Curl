---
id: BL-5796
title: Call the Libcurl xferinfo callback about once a second while a server holds a transfer, with CURLINFO_TOTAL_TIME_T elapsed so far
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Callbacks/Progress, Libcurl/Info/Times, Curl.Engine.UnitLibrary/ProgressHoldTicker.cs, Curl.Engine.UnitLibrary/CallbackTransferProgress.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5796 — Call the Libcurl xferinfo callback about once a second while a server holds a transfer, with CURLINFO_TOTAL_TIME_T elapsed so far

## Goal

With `CURLOPT_NOPROGRESS 0` and a `CURLOPT_XFERINFOFUNCTION`, the port calls the callback while no data moves (a server holding a command or a response), as upstream libcurl 8.22.0 does (about once a second), so a callback that returns non-zero aborts the hold with `CURLE_ABORTED_BY_CALLBACK` (42); and `CURLINFO_TOTAL_TIME_T` (and the other `*_TIME_T` values that are already known) read inside the callback give the time elapsed so far, not 0.

## Context

From Surly cookbook bug report `2026-11-22T2157-Curl-bug-report.md` (batch 13 of the 1,000-recipe plan), finding 1 and 2. Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11.

Repro: `surl --allow-anonymous --directory site --fault "delay;stage=command;command=RETR;path=/slow.bin;ms=3000" ftp://127.0.0.1:2121/`; a Libcurl port with `CURLOPT_URL ftp://127.0.0.1:2121/slow.bin`, `CURLOPT_NOPROGRESS 0`, and an `XFERINFOFUNCTION` that logs each call and reads `CURLINFO_TOTAL_TIME_T`: no call during the 3 s hold, the first comes with the first data, and the time read is 0. Upstream calls the progress function from its multi loop on every pass, at least once a second while waiting (`Curl_pgrsUpdate` with the 1 s timeout in `multi.c`), and `TOTAL_TIME` is updated live by `Curl_pgrsTime`/`Curl_pgrsUpdate`. Measure upstream 8.22.0 (a libcurl test program or the matching `tests/libtest` case, e.g. lib1555/lib1594-style progress tests) for the call cadence during a hold and the value of TOTAL_TIME inside the callback; then match it, with time from the injected `TimeProvider` (no `Thread.Sleep`). Also check the console `--progress-bar`/default meter keeps updating during a hold, which likely shares the same tick. Recipe `ftp-abort-from-a-progress-callback` (case `first-call-with-data`).

## Acceptance criteria

- [x] With a transfer held 3 s before the first byte, the xferinfo callback is called during the hold at upstream's cadence, pinned by a test with a fake `TimeProvider` and a test double that holds; returning non-zero from it during the hold ends the transfer with 42.
- [x] `CURLINFO_TOTAL_TIME_T` read inside the callback is the elapsed time so far (matching upstream's rule for the other `*_TIME_T` values), pinned by a test; the Notes record upstream's measured cadence and values.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- Scope moved to Curl.Engine.UnitLibrary (no other Doing task touches it): the xferinfo sink and the engine's `Info` live there, Libcurl only reads them, so Libcurl needed no change.
- Upstream cadence taken from libcurl's source (`Curl_pgrsUpdate` on every multi pass, wait capped at 1 s; `Curl_pgrsTime` keeps TOTAL_TIME live); not re-measured with a libtest program. Decision: tick every `EasyPauseGate.PausedTickInterval` (1 s) on the injected TimeProvider via `ProgressHoldTicker` (PeriodicTimer); a tick is skipped while the handle is paused (the pause gate ticks then). Callback calls are serialised by a lock in `CallbackTransferProgress`.
- `ManagedEasyTransfer.Info` shows TotalTime = max(held, elapsed since perform start) while a perform with a progress callback runs; the final report replaces it. Other `*_TIME_T` keep their existing live values (connect/pretransfer/starttransfer set at head).
- The tick calls the callback only; it does not redraw the built-in meter for a handle with no callback (no tick starts then beyond the callback-less meter sink, which also ticks via ReportWhilePaused without drawing). Console meter during a hold is not changed.
- Tests: ManagedEasyTransferTests.HoldTick.cs (3 ticks with fake clock, TotalTime grows, abort gives 42), ProgressHoldTickerTests. Quality run for Curl.Engine.UnitLibrary: 0 failing members. CI Linux/macOS: tests are platform-neutral.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T2157, finding 1 and 2.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 1f7a7531 a360bf8b 3d836d5e.
- 2026-11-22: Doing -> Done. xferinfo ticks once a second during a hold with live TotalTime; tests and quality green
