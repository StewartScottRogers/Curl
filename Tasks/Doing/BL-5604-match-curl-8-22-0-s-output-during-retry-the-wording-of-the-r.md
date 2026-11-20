---
id: BL-5604
title: Match curl 8.22.0's output during --retry: the wording of the retry notice and whether a retried attempt's error is printed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/TransferRetryWarning.cs, Curl.Core.UnitLibrary/RetryDiagnosticLog.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5604 — Match curl 8.22.0's output during --retry: the wording of the retry notice and whether a retried attempt's error is printed

## Goal

The port's stderr during and after `--retry` matches curl 8.22.0's byte for byte, with and without `-s`/`-S`.

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #8; Surly cookbook bug report `2026-11-20T0553-Curl-bug-report.md` (batch 3), finding #5. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

Two observations the cookbook asked us to check against upstream:
- `curl -sS --retry 1 --retry-all-errors` against a server that drops the first connection exits 0, but prints the first attempt's `curl: (56) Recv failure` (2203 #8). Upstream prints that error line only when the transfer finally fails, as far as known; measure it.
- The port prints `Retrying in 1 second.`; compare with upstream's wording (0553 #5). curl 8.x prints a `Warning: Problem ... Will retry in 1 second. 1 retry left.` line; its exact text, and whether `-s` or `-sS` hides it, are to be measured.
Measure with `Record-CurlExchange.ps1` against a loopback server that drops the first connection: `-sS --retry 1 --retry-all-errors`, `-s --retry 1`, `--retry 1` (no `-s`), and a final failure after the retries, on Windows and on Linux. Pin each platform's stderr.

## Acceptance criteria

- [ ] The Notes record curl 8.22.0's exact stderr for the four command lines above.
- [ ] The port's stderr and exit code for each of the four match curl's byte for byte, pinned by focused tests in `Curl.Core.UnitTests` (or the console's tests).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (2203 #8, 0553 #5).
- 2026-11-20: Backlog -> Doing. Criteria at claim: bc15ccae 0fcf63cb 3d836d5e.
