---
id: BL-131
title: Rewrite the progress meter status line in place with live counters in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-102, BL-134, BL-130]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-131 — Rewrite the progress meter status line in place with live counters in Curl.Console

## Goal

For a transfer whose handler reports byte counts through the BL-134 sink, `Curl.Console` rewrites the progress meter's status line in place with curl 8.21.0's counters and cadence, so stderr matches curl's bytes for the same reports and clock readings.

## Context

BL-102 writes only the first status line, `\r  0      0   0      0   0      0      0      0                              0`, then `Environment.NewLine` (`Curl.Console/ProgressMeterLines.cs`, `CurlCommandRunner.WriteProgressMeterAsync`). That is byte-identical to curl for `file://`, whose counters stay at zero. For a network transfer curl rewrites the line in place, `\r`-separated with no newline between, and ends with one newline. Measured 2026-09-26, curl 8.21.0 (x86_64-w64-mingw32), local `python -m http.server`, stderr to a file:

- 10-byte file: `\r  0      0   0      0   0      0      0      0                              0\r100     10 100     10   0      0    244      0                              0\r100     10 100     10   0      0    241      0                              0\r100     10 100     10   0      0    238      0                              0\r\n`
- 200000-byte file: lines such as `\r100 195.3k 100 195.3k   0      0 46.52M      0                              0`.

The speed fields depend on timing; sizes use curl's five-character size format (`max5data` in curl's `lib/progress.c`, e.g. `195.3k`, `46.52M`).

**Read the source first.** Before writing code, read curl 8.21.0's `lib/progress.c` (tag `curl-8_21_0` at https://github.com/curl/curl) and record under Notes, with function names: the column layout of the status line, the size format (`max5data`) and its thresholds, how the time columns are filled or left blank, how the Dload/Upload averages and Current speed are computed, and when a line is drawn (the update interval, and the forced draws at the end of a transfer that explain the repeated `100` lines above). Implement what the source says; do not infer it from the two samples.

Constraints: the BL-134 sink is the input (extend the console-side sink BL-130 adds to collect the counts; do not add a second one). All timing reads the injected `TimeProvider` (`ITransferContext.TimeProvider`, which `Curl.Console` already fills); never `Thread.Sleep`. Put the formatting in `Curl.Console/ProgressMeterLines.cs` or a new `Curl.Console` type named for what it formats; every method at cyclomatic complexity 10 or less. Tests use a hand-rolled `TimeProvider` subclass in `Curl.Console.UnitTests` (see `Curl.Networking.UnitTests/Fakes/ManualTimeProvider.cs` for the pattern; test projects do not share fakes) and a fake handler that reports bytes and advances that clock. No package (for example `Microsoft.Extensions.TimeProvider.Testing`) may be added. BL-102's `file://` output must not change.

## Acceptance criteria

- [x] Notes records the `lib/progress.c` rules listed above, citing curl 8.21.0 and the function names.
- [x] Tests in `Curl.Console.UnitTests` pin the five-character size format against curl's `max5data` for at least 0, 10, 99999, 100000, 199999 (`195.3k`), and a speed that formats as `46.52M`.
- [x] A test in `Curl.Console.UnitTests` drives a fake handler reporting 10 of 10 bytes under a manual clock and pins stderr as the two header lines, then the `\r`-separated status lines, then one `Environment.NewLine`, with the percentage and size columns equal to the measurement above (`100     10 100     10   0      0`) and the speed columns computed from the manual clock per the recorded rules.
- [x] A test pins that a handler reporting no bytes still produces exactly BL-102's output (header lines, one zero status line, one newline).
- [x] A test pins that `-s` and `--no-progress-meter` suppress every status line.
- [x] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

### curl 8.21.0 `lib/progress.c` (tag `curl-8_21_0`), read 2026-09-27

- **Column layout** (`progress_meter`): `"
%3d %s %3d %s %3d %s %s %s %s %s %s %s"` - total percent, total expected size, received percent, received size, sent percent, sent size, average download speed, average upload speed, total time, time spent, time left, current speed. Sizes and speeds are `max6out`, times `time2str`. The header lines (and the `** Resuming transfer from byte position N` line) are written once, before the first status line (`headers_out`).
- **Size format** (`max6out`; the task's `max5data` is the name in older curl): under 100000 the number right-aligned in six columns; else divide by 1024 while the quotient is 1000 or more, stepping through `k M G T P E`, then `%2d.%02d` (whole part 99 or less, hundredths = `rest * 100 / 1024`) or `%3d.%d` (tenths = `rest * 10 / 1024`), truncating. 199999 is `195.3k`, 48780288 is `46.52M`.
- **Time columns** (`time2str`, seven columns): zero or less is blank; under 10 hours `  mm:ss` or `h:mm:ss`; up to 99 hours `NNh MMm`; then `%2dd %02dh` up to 99 days, `%6dd` up to 999 days, `%6dm` (days / 30) up to 999 months, `%6dy` (days / 365) up to 99999 years, else `>99999y`. Total time is the larger of the two directions' `total_size / speed` (`pgrs_estimates`, zero unless that size is known and its speed above zero); spent is `timespent / 1000000`; left is total minus spent when total is above zero, else blank.
- **Percentages** (`pgrs_est_percent`): `cur / (total / 100)` for a total over 10000, `cur * 100 / total` above zero, else 0. A direction's percent is 0 unless its size is known and its speed above zero; the total percent uses expected = (upload total if known else uploaded) + (download total if known else downloaded), capped at `CURL_OFF_T_MAX`.
- **Speeds** (`progress_calc`, `trspeed`): the averages are bytes * 1000000 / microseconds since `Curl_pgrsStartNow` (under one microsecond, bytes * 1000000; guarded against overflow). The current speed comes from a ring of `CURL_SPEED_RECORDS` (6) samples of total bytes: at the first call it is the sum of the averages; after that a sample is taken when 1000 ms have passed since the latest, and the speed is (latest - oldest bytes) * 1000000 / their microseconds (at least 1; in `double` when the bytes would overflow), oldest being sample 0 until the ring has wrapped.
- **When a line is drawn** (`progress_calc` returns TRUE): always on the first call; otherwise only after a new sample (1000 ms since the latest), or when the request is done (`req.done`), when a done transfer with no current speed yet overwrites the latest sample instead; and not twice in the same whole second (`lastshow`) unless done. Calls come from `Curl_pgrsCheck` / `Curl_pgrsUpdate` in `transfer.c` (`Curl_sendrecv`) and `multi.c` (the performing and done states), and `Curl_pgrsDone` (`multi_done`) forces a final update then writes `"
"`. A completed transfer gets several done draws in a row - the measurement shows three for a 10-byte HTTP download, each with a fresh clock reading, which is why `244`, `241`, `238` fall. A failed transfer reaches only `Curl_pgrsDone`, with `req.done` false, which draws only a second after the latest sample.

### Decisions (Claude, under Stewart's delegation; the ADR is BL-384)

- **The meter is still written after the transfer**, from the lines `TransferProgressRecorder` drew as the reports came in. Standard error's bytes are curl's; only a terminal differs, seeing the lines at the end rather than moving. Drawing live would need synchronous writes to standard error from inside the handler's reports; the redirected, byte-identical case is what scripts see, so live drawing on a terminal is filed as BL-383.
- **Three done draws after a success, one ordinary update after a failure**, and **none when the handler reported no bytes**: the three is the measured count; a handler that reports no bytes (`file://`, which curl completes without the network transfer loop) keeps BL-102's measured single zero line.
- **The `lastshow` same-second check is not modelled**: every draw of a running transfer here follows a new sample taken 1000 ms or more after the one before, so the whole second always differs and the branch could never be taken (and could not be covered).
- The zero line is drawn when the recorder is made, before any report, as curl draws it at the first update after `Curl_pgrsStartNow`.
- `ProgressMeterLines.Opening` became `HeaderLines` (no zero line): the status lines now come from the recorder. `TransferStartedRecorder` became `TransferProgressRecorder`, as it now records byte counts and draws lines (the one console-side sink, extended as the task asked).
- `Documentation/Planning/Decisions` was not added to `touches`: BL-256, in Doing on another lane, names it, and adding it would send this finished task back to Backlog for one file. The ADR is filed as BL-384 instead.

### Verified 2026-09-27

- `ProgressMeterFieldsTests` pins `max6out` (0, 10, 99999, 100000 `97.65k`, 199999 `195.3k`, 48780288 `46.52M`, the unit step and `long.MaxValue` `7.99E`), every `time2str` branch, percent and `trspeed`.
- `TransferProgressRecorderTests` pins the zero line, no draw within a second, three done lines for 10 of 10 bytes in 40 ms (`250`), a failed draw after two seconds with time columns, the six-sample ring (current speed 9000 at 7 s), a kept current speed on done, and the overflow and capped-total paths.
- `CurlCommandRunnerLiveProgressMeterTests` pins stderr for 10 of 10 bytes (headers, zero line, three `100     10 100     10   0      0    250 ...` lines, one `Environment.NewLine`), a handler reporting no bytes (BL-102's bytes), `-s` and `--no-progress-meter` (no status lines), and two transfers each drawn from their own reports.
- `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean on both projects; fast tests green (Curl.Console.UnitTests 722 passed, whole solution green); `TransferProgressRecorder.cs`, `ProgressMeterFields.cs`, `ProgressMeterLines.cs` at 100% line and branch coverage; no new Integration test.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console draws the progress meter's status lines from the handler's byte reports on the injected clock, as curl 8.21.0's progress.c does
