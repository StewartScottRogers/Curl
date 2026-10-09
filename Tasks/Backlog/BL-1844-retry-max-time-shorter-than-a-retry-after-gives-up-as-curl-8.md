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
completed:
---
# BL-1844 — --retry-max-time shorter than a Retry-After gives up as curl 8.21.0 does (upstream test366)

## Goal

`curl http://host/366 --retry 2 --retry-max-time 10` against a 503 with `Retry-After: 200` writes the standard error curl 8.21.0 writes and exits 0, so the gap item `behaviour:test366` measures `match`.

## Context

- Split from BL-1808 (gap finding GF-0015, item `behaviour:test366`); read BL-1808's Notes first.
- Upstream: https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/test366 (one GET only; no retry is made).
- Curl prints `TransferRetryWarning.RetryAfterExceedsMaxTime` through `TransferRetrier`'s `retriesAbandoned` callback (`CurlCommandRunner.FollowRetryingAsync`, `WriteWarningUnlessSilentAsync`). BL-317 measured that warning on curl 8.21.0 (Windows build); the gap office says the reference curl's stderr differs. Measure both builds with `Record-CurlExchange.ps1` and compare the exact text (wording, wrapping, the 503 progress meter) before changing anything; the fix may be text, not removal.

## Acceptance criteria

- [ ] A unit test pins curl 8.21.0's standard error and exit 0 for test366's exchange.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
