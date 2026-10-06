---
id: BL-390
title: Retry a refused connect under --retry-connrefused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-391, BL-317]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-390 — Retry a refused connect under --retry-connrefused

## Goal

`TransferRetrier` retries a refused connect under `--retry-connrefused` with `Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.`, and no other exit 7.

## Context

- Split from BL-317 (2026-09-27) because the retrier cannot tell a refused connect from another exit 7 until BL-391 carries it.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27: `curl --retry 2 --retry-connrefused http://127.0.0.1:1/` printed `curl: (7) Failed to connect to 127.0.0.1:1 after 2044 ms: Could not connect to server`, then `Warning: Problem : connection refused. Retrying in 1 second. 2 retries left.`, again with `Retrying in 2 seconds. 1 retry left.`, final exit 7. `http://0.0.0.0:1/` (exit 7, not refused) was not retried.
- Upstream order (`retrycheck`): timeout family, then connection refused, then HTTP, then FTP, then `--retry-all-errors`; an exit 7 that is not refused under `--retry-connrefused` falls through to `--retry-all-errors` only. Add `RetryPolicy.RetryConnectionRefused` and `TransferRetryReason.ConnectionRefused`.

## Acceptance criteria

- [x] `TransferRetrierTests` pin the refused retry, its warnings and backoff, and that a non-refused exit 7 is final without `--retry-all-errors`, on `FakeTimeProvider`.
- [x] `dotnet build` is clean with warnings as errors; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library changed.

## Notes

- Delivered: `RetryPolicy.RetryConnectionRefused`; `TransferRetryReason.ConnectionRefused`, printed by `TransferRetryWarning` as `: connection refused`; `TransferRetrier` checks it after the timeout family and before the HTTP and FTP rules, as upstream `retrycheck` does. It retries only when the policy asks, the exit is 7 and `TransferResult.IsConnectionRefused` is set (the exit-code check guards a handler that sets the flag on another failure). A non-refused exit 7 falls through to `--retry-all-errors` only. The behaviour is curl 8.21.0's, measured in BL-317's notes, so it needs no ADR.
- `TransferRetryWarning.For` reached cyclomatic complexity 11 with the new reason; the reason-to-text switch moved into a private `Problem` method.
- Curl.Core.UnitTests: 856 passed, 4 skipped (POSIX-only). `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. `dotnet format --verify-no-changes` reports nothing in Curl.Core; its ENDOFLINE errors in `Curl.Output.UnitLibrary\X509CertificateFields.cs` were already there and are outside this task.
- Follow-up: `Curl.Console`'s `RetryPolicyMapping` does not copy the option yet (outside `touches`); filed as BL-433.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TransferRetrier retries a refused connect under --retry-connrefused with curl 8.21.0's ': connection refused' warning and backoff; other exit 7 stays final
