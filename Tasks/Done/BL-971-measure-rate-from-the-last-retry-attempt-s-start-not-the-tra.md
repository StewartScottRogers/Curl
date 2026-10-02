---
id: BL-971
title: Measure --rate from the last retry attempt's start, not the transfer's first
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-650]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-02
---
# BL-971 — Measure --rate from the last retry attempt's start, not the transfer's first

## Goal

With `--retry` and `--rate` together, the `--rate` wait before the next transfer counts from the start of the transfer's last attempt, as curl 8.21.0's `serial_transfers` does (it resets `start` on every retry), so the `Note: Transfer took <n> ms, waits <m>ms as set by --rate` numbers match curl's.

## Context

- BL-650 added `--rate`: `CurlCommandRunner.WaitForTransferStartRateAsync` measures from `previousSerialTransferStart`, set once before `TransferAndReportAsync`, so a retried transfer is measured from its first attempt. Retries happen inside `TransferRetrier` (Curl.Core), so the runner needs the last attempt's start from there or from its own per-attempt hook.
- Measure first with `Record-CurlExchange.ps1`: e.g. `-v --retry 1 --retry-delay 1 --rate 1/5s` over two URLs whose first answers 503 once; copy the Note lines and run time into Notes.

## Acceptance criteria

- [x] Measured first, as above, with the Note lines and run time copied into Notes.
- [x] A `Curl.Console.UnitTests` test on a fake `TimeProvider` pins the wait and the Note after a retried transfer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for Curl.Console.

## Notes

- Measured 2026-10-02 with the local curl 8.21.0 (Schannel) through `Record-CurlExchange.ps1 -Connections 3 -ResponseDelayMilliseconds 300`, answering 503 (`Connection: close`) then 200 for every later connection, with `-v -s -o NUL --retry 1 --retry-delay 1 --rate 1/5s http://127.0.0.1:18971/a http://127.0.0.1:18971/b`: exit 0, run time 6728 ms, and the one Note line was `Note: Transfer took 315 ms, waits 4685ms as set by --rate`. 315 ms is the second attempt alone (a 300 ms answer), not 300 + 1000 ms delay + 300, so curl's start is reset after the retry wait, as the last attempt starts.
- Change: `CurlCommandRunner.RecordSerialAttemptStart` runs as every attempt starts inside `FollowRetryingAsync`'s retrier callback (after the retry wait and the retry lines) and moves `previousSerialTransferStart` to now, unless the run is `-Z`. `RunTransferAsync` still sets it before each transfer, which covers transfers that never reach the retrier. No Curl.Core change was needed, so `touches` stayed as filed.
- Test: `CurlCommandRunnerRateTests.RunAsync_RetriedTransfer_WaitsFromTheLastAttemptsStart` (300 ms attempts, a timeout then success, `--retry-delay 1 --rate 1/5s`): starts at 0, 1300 and 6300 ms; waits 1 s and 4700 ms; Note `Transfer took 300 ms, waits 4700ms as set by --rate`.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green; Measure-CodeQuality: Curl.Console 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --rate measures from the last --retry attempt's start, matching curl 8.21.0's Note
