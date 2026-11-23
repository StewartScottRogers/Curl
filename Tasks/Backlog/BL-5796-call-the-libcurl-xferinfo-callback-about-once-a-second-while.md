---
id: BL-5796
title: Call the Libcurl xferinfo callback about once a second while a server holds a transfer, with CURLINFO_TOTAL_TIME_T elapsed so far
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Callbacks/Progress, Libcurl/Info/Times]
requirement: none
created: 2026-11-22
completed:
---
# BL-5796 — Call the Libcurl xferinfo callback about once a second while a server holds a transfer, with CURLINFO_TOTAL_TIME_T elapsed so far

## Goal

With `CURLOPT_NOPROGRESS 0` and a `CURLOPT_XFERINFOFUNCTION`, the port calls the callback while no data moves (a server holding a command or a response), as upstream libcurl 8.22.0 does (about once a second), so a callback that returns non-zero aborts the hold with `CURLE_ABORTED_BY_CALLBACK` (42); and `CURLINFO_TOTAL_TIME_T` (and the other `*_TIME_T` values that are already known) read inside the callback give the time elapsed so far, not 0.

## Context

From Surly cookbook bug report `2026-11-22T2157-Curl-bug-report.md` (batch 13 of the 1,000-recipe plan), finding 1 and 2. Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11.

Repro: `surl --allow-anonymous --directory site --fault "delay;stage=command;command=RETR;path=/slow.bin;ms=3000" ftp://127.0.0.1:2121/`; a Libcurl port with `CURLOPT_URL ftp://127.0.0.1:2121/slow.bin`, `CURLOPT_NOPROGRESS 0`, and an `XFERINFOFUNCTION` that logs each call and reads `CURLINFO_TOTAL_TIME_T`: no call during the 3 s hold, the first comes with the first data, and the time read is 0. Upstream calls the progress function from its multi loop on every pass, at least once a second while waiting (`Curl_pgrsUpdate` with the 1 s timeout in `multi.c`), and `TOTAL_TIME` is updated live by `Curl_pgrsTime`/`Curl_pgrsUpdate`. Measure upstream 8.22.0 (a libcurl test program or the matching `tests/libtest` case, e.g. lib1555/lib1594-style progress tests) for the call cadence during a hold and the value of TOTAL_TIME inside the callback; then match it, with time from the injected `TimeProvider` (no `Thread.Sleep`). Also check the console `--progress-bar`/default meter keeps updating during a hold, which likely shares the same tick. Recipe `ftp-abort-from-a-progress-callback` (case `first-call-with-data`).

## Acceptance criteria

- [ ] With a transfer held 3 s before the first byte, the xferinfo callback is called during the hold at upstream's cadence, pinned by a test with a fake `TimeProvider` and a test double that holds; returning non-zero from it during the hold ends the transfer with 42.
- [ ] `CURLINFO_TOTAL_TIME_T` read inside the callback is the elapsed time so far (matching upstream's rule for the other `*_TIME_T` values), pinned by a test; the Notes record upstream's measured cadence and values.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T2157, finding 1 and 2.
