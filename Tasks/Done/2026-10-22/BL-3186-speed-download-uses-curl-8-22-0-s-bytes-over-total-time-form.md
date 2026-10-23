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
completed: 2026-10-22
---
# BL-3186 — %{speed_download} uses curl 8.22.0's bytes-over-total-time formula (BL-3171 item 4)

## Goal

`%{speed_download}` uses curl 8.22.0's formula, bytes over total time, with its divisor and rounding.

## Context

BL-3171 item 4. `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` / `TransferWriteOutVariables.cs`. Measure where Curl differs (divisor, rounding, zero time) against upstream's `trspeed` and fix it.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [x] A test in `Curl.Output.UnitTests/WriteOutSpeedDownloadTests.cs` pins `%{speed_download}` for sizes and total times (including a sub-second total, zero total time and a large size) to the values curl 8.22.0 prints.
- [x] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

- 2026-10-22 (lane 7): Measured curl 8.22.0 Schannel with Record-CurlExchange.ps1 -NoServer, `-w "%{size_download} %{time_total} %{speed_download}"`: file:// 1 byte printed `1 0.000854 1190`, `1 0.000738 1377`, `1 0.000813 1250`; file:// 5000000 bytes printed `5000000 0.011645 430107526`, `5000000 0.003532 1422879908`, `5000000 0.002570 1959247648`; a refused connect (http://127.0.0.1:1/) printed `0 0.000645 0 0`, exit 7. Every speed is bytes x 1000000 over a divisor in microseconds, truncated (430107526 = 5e12 / 11625); the divisor is curl's last progress update, a few microseconds before time_total, as ADR-0035 already records. Curl's `BytesPerSecond` already is that formula (truncation, the overflow branch for a size too large to scale, long.MaxValue under one second), so no production change was needed: the new tests pin it with the divisors curl used.
- Decision (sensible default, no new ADR; ADR-0035 covers it): a zero total time happens in Curl only for a report without timings, and it keeps printing 0 rather than trspeed's `size * 1000000` for `us < 1`. curl itself never reports a zero time (Curl_pgrsTime makes every event at least 1 us, and even a refused connect measured 0.000645 with speed 0), so 0 is what curl prints for a transfer with no measured time and no bytes, and an unmeasured time should give no speed rather than a million times the size.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary: line 100, branch 100, 495 members, 0 failing, worst CRAP 10. Curl.Output.UnitTests fast tests: 789 passed, 3 skipped. The tests are pure computation over a fake clock, so platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 7b11e7de 6361b4ee.
- 2026-10-22: Doing -> Done. %{speed_download} pinned to curl 8.22.0's trspeed: bytes x 1e6 over total microseconds, truncated, with the overflow branch and 0 for zero time
