---
id: BL-131
title: Rewrite the progress meter status line in place with live counters in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-102, BL-128, BL-130]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-131 — Rewrite the progress meter status line in place with live counters in Curl.Console

## Goal

For a transfer whose handler reports byte counts through the BL-128 sink, `Curl.Console` rewrites the progress meter's status line in place with curl 8.21.0's counters and cadence, so stderr matches curl's bytes for the same reports and clock readings.

## Context

BL-102 writes only the first status line, `\r  0      0   0      0   0      0      0      0                              0`, then `Environment.NewLine` (`Curl.Console/ProgressMeterLines.cs`, `CurlCommandRunner.WriteProgressMeterAsync`). That is byte-identical to curl for `file://`, whose counters stay at zero. For a network transfer curl rewrites the line in place, `\r`-separated with no newline between, and ends with one newline. Measured 2026-09-26, curl 8.21.0 (x86_64-w64-mingw32), local `python -m http.server`, stderr to a file:

- 10-byte file: `\r  0      0   0      0   0      0      0      0                              0\r100     10 100     10   0      0    244      0                              0\r100     10 100     10   0      0    241      0                              0\r100     10 100     10   0      0    238      0                              0\r\n`
- 200000-byte file: lines such as `\r100 195.3k 100 195.3k   0      0 46.52M      0                              0`.

The speed fields depend on timing; sizes use curl's five-character size format (`max5data` in curl's `lib/progress.c`, e.g. `195.3k`, `46.52M`).

**Read the source first.** Before writing code, read curl 8.21.0's `lib/progress.c` (tag `curl-8_21_0` at https://github.com/curl/curl) and record under Notes, with function names: the column layout of the status line, the size format (`max5data`) and its thresholds, how the time columns are filled or left blank, how the Dload/Upload averages and Current speed are computed, and when a line is drawn (the update interval, and the forced draws at the end of a transfer that explain the repeated `100` lines above). Implement what the source says; do not infer it from the two samples.

Constraints: the BL-128 sink is the input (extend the console-side sink BL-130 adds to collect the counts; do not add a second one). All timing reads the injected `TimeProvider` (`ITransferContext.TimeProvider`, which `Curl.Console` already fills); never `Thread.Sleep`. Put the formatting in `Curl.Console/ProgressMeterLines.cs` or a new `Curl.Console` type named for what it formats; every method at cyclomatic complexity 10 or less. Tests use a hand-rolled `TimeProvider` subclass in `Curl.Console.UnitTests` (see `Curl.Networking.UnitTests/Fakes/ManualTimeProvider.cs` for the pattern; test projects do not share fakes) and a fake handler that reports bytes and advances that clock. No package (for example `Microsoft.Extensions.TimeProvider.Testing`) may be added. BL-102's `file://` output must not change.

## Acceptance criteria

- [ ] Notes records the `lib/progress.c` rules listed above, citing curl 8.21.0 and the function names.
- [ ] Tests in `Curl.Console.UnitTests` pin the five-character size format against curl's `max5data` for at least 0, 10, 99999, 100000, 199999 (`195.3k`), and a speed that formats as `46.52M`.
- [ ] A test in `Curl.Console.UnitTests` drives a fake handler reporting 10 of 10 bytes under a manual clock and pins stderr as the two header lines, then the `\r`-separated status lines, then one `Environment.NewLine`, with the percentage and size columns equal to the measurement above (`100     10 100     10   0      0`) and the speed columns computed from the manual clock per the recorded rules.
- [ ] A test pins that a handler reporting no bytes still produces exactly BL-102's output (header lines, one zero status line, one newline).
- [ ] A test pins that `-s` and `--no-progress-meter` suppress every status line.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

## Log

- 2026-09-26: Created.
