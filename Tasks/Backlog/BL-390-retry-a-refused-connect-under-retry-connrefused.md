---
id: BL-390
title: Retry a refused connect under --retry-connrefused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-388, BL-317]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-390 — Retry a refused connect under --retry-connrefused

## Goal

`TransferRetrier` retries a refused connect under `--retry-connrefused` with `Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.`, and no other exit 7.

## Context

- Split from BL-317 (2026-09-27) because the retrier cannot tell a refused connect from another exit 7 until BL-388 carries it.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27: `curl --retry 2 --retry-connrefused http://127.0.0.1:1/` printed `curl: (7) Failed to connect to 127.0.0.1:1 after 2044 ms: Could not connect to server`, then `Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.`, again with `Retrying in 2 seconds. 1 retry left.`, final exit 7. `http://0.0.0.0:1/` (exit 7, not refused) was not retried.
- Upstream order (`retrycheck`): timeout family, then connection refused, then HTTP, then FTP, then `--retry-all-errors`; an exit 7 that is not refused under `--retry-connrefused` falls through to `--retry-all-errors` only. Add `RetryPolicy.RetryConnectionRefused` and `TransferRetryReason.ConnectionRefused`.

## Acceptance criteria

- [ ] `TransferRetrierTests` pin the refused retry, its warnings and backoff, and that a non-refused exit 7 is final without `--retry-all-errors`, on `FakeTimeProvider`.
- [ ] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

## Log

- 2026-09-27: Created.
