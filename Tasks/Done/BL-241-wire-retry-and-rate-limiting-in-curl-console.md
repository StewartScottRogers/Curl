---
id: BL-241
title: Wire retry and rate limiting in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-208, BL-209, BL-196, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-241 — Wire retry and rate limiting in Curl.Console

## Goal

`--retry*` and `--limit-rate` wrap transfers with BL-208 and BL-209 in `Curl.Console`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W12. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-230 added as a dependency beyond the plan. `-Y`/`-y` (speed limit) have no behaviour task yet; see Notes.

## Acceptance criteria

- [x] A retried transfer prints the measured warning and succeeds on the second attempt over a fake handler on `FakeTimeProvider`.
- [x] `--limit-rate` wraps the output stream with BL-209's limiter.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- `-Y`/`--speed-limit` and `-y`/`--speed-time` are parsed by BL-196 but no task implements the low-speed abort (exit 28); file one if it is still missing when this runs.
- Plan item: W12 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-208 (2026-09-26): wrap the transfer in `Curl.Core.TransferRetrier` with a `RetryPolicy` from `--retry`/`--retry-delay`. Its callback gets each retried attempt and the unwrapped warning line. Measured on curl 8.21.0: each failed attempt's `curl: (N) ...` line is printed before its warning; `-s`/`-sS` print no warning; the warning is wrapped at the terminal width like every `Warning:` line (`WarningLineWrapper`); every attempt's body is written to stdout (`--retry 1` on a 503 printed the body twice); and the exit code is the last attempt's (0 for a 503 without `-f`). The `-o` file handling between attempts (curl truncates it) still needs measuring.
- From BL-317 (2026-09-27): `RetryPolicy` also takes `MaxTime` (`--retry-max-time`) and `RetryAllErrors` (`--retry-all-errors`), and `TransferRetrier.RunAsync` takes a fourth callback, `retriesAbandoned`, with the `Retry-After`-past-max-time warning to print (unless silenced) instead of retrying. `--retry-connrefused` waits on BL-390.
- Measured 2026-09-27, curl 8.21.0 (Windows, Schannel, `C:\windows\system32\curl.exe`) with `Record-CurlExchange.ps1 -Connections 2` answering `503` with `busy`, `--retry 1 --retry-delay 1`:
  - to stdout: body `busybusy`, exit 0; stderr is the meter header once, the first attempt's status lines and newline, `Warning: Problem : HTTP error. Retrying in 1 second. 1 retry left.`, then the second attempt's status lines and newline. The meter header is written once per transfer, not per attempt.
  - `-o out.txt`: the file holds one `busy`; curl truncates the `-o` file to its length at open before retrying (`DeferredOutputFileStream.TruncateForRetry`).
  - `-f -o`: meter header, status line, `curl: (22) The requested URL returned error: 503`, warning, status line, the `(22)` line again; exit 22; no file.
  - `-#`: the bar's drawing, then the warning with no newline between, then the second attempt's bar.
  - `-sS -f`: both `(22)` lines, no warning.
- Choices (defaults taken, no ADR needed - each follows measured curl or an existing rule):
  - Each attempt gets a fresh `TransferContext` (fresh progress recorder and rate limiter) built by the same factory; the retrier's own context is the first attempt's.
  - The retry lines are written from the retrier's synchronous callback as a task the next attempt awaits first, so they appear before the wait as curl prints them.
  - `TransferContextFactory` now gives every context the runner's `TimeProvider` (it was `TimeProvider.System`), so the retry waits, the rate limiter and the handlers' deadlines share the injected clock.
  - `--limit-rate 0` sets no limit, as `CommandLineOptions.LimitRate` documents for curl.
  - Tests run on a new `ImmediateTimerTimeProvider` (test project): the repository has no `FakeTimeProvider` in `Curl.Console.UnitTests`, and `ManualTimeProvider` there has no timers. Timers due in a minute or more never fire, so HTTP's 300-second default connect timeout does not end the test transfers.
- `DiskWriteOutFileOpener.TryOpen` is covered only by Integration tests (BL-280), so `Measure-CodeQuality.ps1 -Library Curl.Console` without `-IncludeIntegration` reports it; with `-IncludeIntegration` Curl.Console is 100% line and branch, 0 failing members, worst CRAP 10.
- Filed BL-400 for the `-Y`/`-y` low-speed abort, which nothing implemented yet.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --retry and --limit-rate wrap every transfer in Curl.Console; measured curl output pinned; Curl.Console 100% line and branch
