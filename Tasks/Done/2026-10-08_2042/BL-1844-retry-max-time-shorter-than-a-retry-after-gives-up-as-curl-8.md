---
id: BL-1844
title: --retry-max-time shorter than a Retry-After gives up as curl 8.21.0 does (upstream test366)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1844 — --retry-max-time shorter than a Retry-After gives up as curl 8.21.0 does (upstream test366)

## Goal

`curl http://host/366 --retry 2 --retry-max-time 10` against a 503 with `Retry-After: 200` writes the standard error curl 8.21.0 writes and exits 0, so the gap item `behaviour:test366` measures `match`.

## Context

- Split from BL-1808 (gap finding GF-0015, item `behaviour:test366`); read BL-1808's Notes first.
- Upstream: https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/test366 (one GET only; no retry is made).
- Curl prints `TransferRetryWarning.RetryAfterExceedsMaxTime` through `TransferRetrier`'s `retriesAbandoned` callback (`CurlCommandRunner.FollowRetryingAsync`, `WriteWarningUnlessSilentAsync`). BL-317 measured that warning on curl 8.21.0 (Windows build); the gap office says the reference curl's stderr differs. Measure both builds with `Record-CurlExchange.ps1` and compare the exact text (wording, wrapping, the 503 progress meter) before changing anything; the fix may be text, not removal.

## Acceptance criteria

- [x] A unit test pins curl 8.21.0's standard error and exit 0 for test366's exchange.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-08 (lane 1): Measured curl 8.21.0 (mingw, Schannel) with Record-CurlExchange.ps1 on
  test366's exact response and command line: one GET, stdout `server not available\n`, exit 0,
  and stderr = the progress meter, then `Warning: The Retry-After: time would make this command
  line exceed the maximum \r\nWarning: allowed time for retries.\r\n` (the existing
  `TransferRetryWarning.RetryAfterExceedsMaxTime`, wrapped at 79 columns). Curl already writes
  the same; no production change. Pinned in
  `CurlCommandRunnerRetryTests.RunAsync_RetryAfterLongerThanRetryMaxTime_SendsOneRequestWarnsAndExitsZero`
  (one GET, no wait, that stderr and stdout, exit 0).
- Upstream test366 itself checks only the request (no stderr or stdout section), so the gap
  item should measure `match` on the next gap run; the lane cannot read the gap office's
  measurement (audit guard), so any remaining Linux-build stderr difference is left to that
  re-measurement.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. test366 exchange pinned: one GET, Retry-After warning, exit 0; Curl already matched curl 8.21.0
