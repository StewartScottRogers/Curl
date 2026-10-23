---
id: BL-3186
title: %{speed_download} uses curl 8.22.0's bytes-over-total-time formula (BL-3171 item 4)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/WriteOutSpeedDownloadTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3186 — %{speed_download} uses curl 8.22.0's bytes-over-total-time formula (BL-3171 item 4)

## Goal

`%{speed_download}` uses curl 8.22.0's formula, bytes over total time, with its divisor and rounding.

## Context

BL-3171 item 4. `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` / `TransferWriteOutVariables.cs`. Measure where Curl differs (divisor, rounding, zero time) against upstream's `trspeed` and fix it.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [ ] A test in `Curl.Output.UnitTests/WriteOutSpeedDownloadTests.cs` pins `%{speed_download}` for sizes and total times (including a sub-second total, zero total time and a large size) to the values curl 8.22.0 prints.
- [ ] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 7b11e7de 6361b4ee.
