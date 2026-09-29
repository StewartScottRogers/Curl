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
completed:
---
# BL-971 — Measure --rate from the last retry attempt's start, not the transfer's first

## Goal

With `--retry` and `--rate` together, the `--rate` wait before the next transfer counts from the start of the transfer's last attempt, as curl 8.21.0's `serial_transfers` does (it resets `start` on every retry), so the `Note: Transfer took <n> ms, waits <m>ms as set by --rate` numbers match curl's.

## Context

- BL-650 added `--rate`: `CurlCommandRunner.WaitForTransferStartRateAsync` measures from `previousSerialTransferStart`, set once before `TransferAndReportAsync`, so a retried transfer is measured from its first attempt. Retries happen inside `TransferRetrier` (Curl.Core), so the runner needs the last attempt's start from there or from its own per-attempt hook.
- Measure first with `Record-CurlExchange.ps1`: e.g. `-v --retry 1 --retry-delay 1 --rate 1/5s` over two URLs whose first answers 503 once; copy the Note lines and run time into Notes.

## Acceptance criteria

- [ ] Measured first, as above, with the Note lines and run time copied into Notes.
- [ ] A `Curl.Console.UnitTests` test on a fake `TimeProvider` pins the wait and the Note after a retried transfer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for Curl.Console.

## Notes

## Log

- 2026-09-29: Created.
