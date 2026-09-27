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
completed:
---
# BL-241 — Wire retry and rate limiting in Curl.Console

## Goal

`--retry*` and `--limit-rate` wrap transfers with BL-208 and BL-209 in `Curl.Console`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W12. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-230 added as a dependency beyond the plan. `-Y`/`-y` (speed limit) have no behaviour task yet; see Notes.

## Acceptance criteria

- [ ] A retried transfer prints the measured warning and succeeds on the second attempt over a fake handler on `FakeTimeProvider`.
- [ ] `--limit-rate` wraps the output stream with BL-209's limiter.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- `-Y`/`--speed-limit` and `-y`/`--speed-time` are parsed by BL-196 but no task implements the low-speed abort (exit 28); file one if it is still missing when this runs.
- Plan item: W12 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-208 (2026-09-26): wrap the transfer in `Curl.Core.TransferRetrier` with a `RetryPolicy` from `--retry`/`--retry-delay`. Its callback gets each retried attempt and the unwrapped warning line. Measured on curl 8.21.0: each failed attempt's `curl: (N) ...` line is printed before its warning; `-s`/`-sS` print no warning; the warning is wrapped at the terminal width like every `Warning:` line (`WarningLineWrapper`); every attempt's body is written to stdout (`--retry 1` on a 503 printed the body twice); and the exit code is the last attempt's (0 for a 503 without `-f`). The `-o` file handling between attempts (curl truncates it) still needs measuring.
- From BL-317 (2026-09-27): `RetryPolicy` also takes `MaxTime` (`--retry-max-time`) and `RetryAllErrors` (`--retry-all-errors`), and `TransferRetrier.RunAsync` takes a fourth callback, `retriesAbandoned`, with the `Retry-After`-past-max-time warning to print (unless silenced) instead of retrying. `--retry-connrefused` waits on BL-390.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
