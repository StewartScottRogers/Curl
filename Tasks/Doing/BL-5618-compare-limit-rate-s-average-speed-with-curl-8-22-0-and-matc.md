---
id: BL-5618
title: Compare --limit-rate's average speed with curl 8.22.0 and match its overshoot
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RateLimitedStream.cs, Curl.Core.UnitLibrary/TransferRateLimit.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5618 — Compare --limit-rate's average speed with curl 8.22.0 and match its overshoot

## Goal

Over the same transfer, the port's average download speed under `--limit-rate` is as close to the limit as curl 8.22.0's, and its first-second burst behaves the same.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 8. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`--limit-rate 25k` on a 100000-byte download averaged about 33500 B/s, a third over the limit. curl's limiter (8.x `Curl_pgrsLimitWaitTime`) also lets the start burst a little; measure curl 8.22.0 and the port side by side on the same loopback server for 100 KB and 1 MB at 25k and 100k, five runs each, and compare averages. Speed itself is not a goal (no speed parity with C curl), but a rate limit that overshoots by a third is behaviour a user sets the option to control. Use an injected `TimeProvider` in any unit test, never real sleeps.

## Acceptance criteria

- [ ] The Notes record curl 8.22.0's and the port's average speeds for the four cases above.
- [ ] The port's average is within 10 percent of the limit whenever curl's is, pinned by a unit test of the limiter with a fake clock.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 8.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 199df755 1eb9a7cc 3d836d5e.
