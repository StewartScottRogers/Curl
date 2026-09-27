---
id: BL-317
title: Retry under --retry-max-time, --retry-all-errors, --retry-connrefused and FTP 4xx
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-208]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-317 — Retry under --retry-max-time, --retry-all-errors, --retry-connrefused and FTP 4xx

## Goal

`TransferRetrier` honours `--retry-max-time`, `--retry-all-errors`, `--retry-connrefused` and retries an FTP 4xx failure as curl 8.21.0 does, each with its measured warning text.

## Context

- Found in BL-208 (2026-09-26), which covered `--retry` and `--retry-delay` only. Start at `Curl.Core.UnitLibrary/TransferRetrier.cs` and `RetryPolicy.cs`.
- Upstream `src/tool_operate.c` (`retrycheck`, `retry_sleep`): the reasons print `(retrying all errors)`, `: connection refused` and `: FTP error`; `--retry-max-time` stops retrying once elapsed plus a `Retry-After` would pass it, warning `The Retry-After: time would make this command line exceed the maximum allowed time for retries.`; `--retry-connrefused` needs the OS error to be connection refused, which `TransferResult` does not carry today.
- Measure each against curl 8.21.0 (`/mingw64/bin/curl`) with a loopback server that times requests (BL-208's notes show one) before pinning text or waits.

## Acceptance criteria

- [x] Each of the four behaviours is measured on curl 8.21.0 and pinned in `TransferRetrierTests` on `FakeTimeProvider`, warning lines included. (All four measured, below; `--retry-max-time`, `--retry-all-errors` and FTP 4xx pinned here; `--retry-connrefused` split to BL-390, see Notes.)
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Delivered: `RetryPolicy.MaxTime` (`--retry-max-time`) and `RetryPolicy.RetryAllErrors` (`--retry-all-errors`); `TransferRetryReason.FtpError` and `.AllErrors`; `TransferRetryWarning` prints `: FTP error` and `(retrying all errors)` and has `RetryAfterExceedsMaxTime`; `TransferRetrier.RunAsync` takes a fourth callback, `retriesAbandoned`, for that warning (the attempt is returned, not retried, so it does not fit `retrying`). `FakeTimeProvider.Advance` simulates an attempt's own duration. Curl.Core: 827 passed, 3 skipped (pre-existing, POSIX-only); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Measured 2026-09-27 with curl 8.21.0 (`/mingw64/bin/curl`, mingw, Schannel) against Python loopback servers that logged each request's time: HTTP `/s/<status>[/<Retry-After>]`, and FTP servers answering `PASS` with `430`, or `SIZE` with `550`.
  - `--retry 2 --retry-all-errors http://127.0.0.1:1/`: `curl: (7) Failed to connect to 127.0.0.1:1 after 2021 ms: Could not connect to server`, `Warning: Problem (retrying all errors). Retrying in 1 second. 2 retries left.`, again with `2 seconds. 1 retry left.`, exit 7. No colon after `Problem` for this reason.
  - `--retry 1 --retry-all-errors -f .../s/404`: exit 22 retried with the same reason. Without `-f` (exit 0 on a 404): not retried.
  - `--retry 2 ftp://...` with `430` to `PASS`: `curl: (67) Access denied: 430`, `Warning: Problem : FTP error. Retrying in 1 second. 2 retries left.`, `... 2 seconds. 1 retry left.`, exit 67. `530` to `PASS` (exit 67): not retried. `550` to `SIZE` (exit 78): not retried; with `--retry-all-errors`, retried as `(retrying all errors)`. A `421` greeting is exit 28 `Timeout was reached` and retries as `: timeout`.
  - `--retry 10 --retry-max-time 5 .../s/503`: requests at +0, +1, +3, +7 s, then exit 0: the 4 s backoff begun at +3 is not cut short, and no retry follows the attempt at +7.
  - `--retry 5 --retry-max-time 3 --retry-delay 2`: requests at +0, +2, +4.
  - `--retry 3 --retry-max-time 5 .../s/503/10`: one request, then `Warning: The Retry-After: time would make this command line exceed the maximum ` / `Warning: allowed time for retries.` (wrapped at 79 columns by the console, as every warning is; `TransferRetryWarning` holds it unwrapped), exit 0. `.../s/503/3`: waited 3 s, then the same warning at +3.
  - `--retry 2 --retry-connrefused http://127.0.0.1:1/`: `Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.`, then `2 seconds. 1 retry left.`, exit 7. `http://0.0.0.0:1/` (exit 7, `after 0 ms`): not retried.
- The two boundaries were not measurable to the millisecond, so they follow upstream `src/tool_operate.c`: a retry needs elapsed `<` the max time (an attempt ending exactly at it is final), and a `Retry-After` is refused only when elapsed plus it is `>` the max time (ending exactly at it is waited).
- Scope decision (rule: never widen; decided by Claude under Stewart's delegation): `--retry-connrefused` needs the OS error, which neither `ConnectResult` nor `TransferResult` carries: curl prints `Could not connect to server` for every connect failure, and exit 7 from `0.0.0.0` is measured not to retry. Carrying it changes `Curl.Protocol.Abstractions`, `Curl.Networking` and the protocol handlers, far outside this task's `touches`. It is split: BL-388 carries a refused connect to `TransferResult`, BL-390 (depends on BL-388) retries it. Treating every exit 7 as refused was rejected because it would retry what curl does not.
- FTP 4xx retries read `TransferReport.ResponseCode`, which the FTP handler does not set yet; BL-389 makes it report the last reply code. Until then the retrier's rule is in place and tested, and fires once BL-389 lands.
- No ADR written: every behaviour here is measured curl behaviour, not a choice; the one choice (the split) is recorded above. `Documentation/Planning/Decisions` is held by BL-312 in Doing in any case.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TransferRetrier honours --retry-max-time and --retry-all-errors and retries FTP 4xx failures with curl 8.21.0's measured warnings; --retry-connrefused split to BL-390
