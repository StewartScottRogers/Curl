---
id: BL-5618
title: Compare --limit-rate's average speed with curl 8.22.0 and match its overshoot
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RateLimitedStream.cs, Curl.Core.UnitLibrary/TransferRateLimit.cs, Curl.Core.UnitTests/RateLimitedStreamTests.cs, Curl.Console.UnitTests/CurlCommandRunnerRetryTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5618 — Compare --limit-rate's average speed with curl 8.22.0 and match its overshoot

## Goal

Over the same transfer, the port's average download speed under `--limit-rate` is as close to the limit as curl 8.22.0's, and its first-second burst behaves the same.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 8. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`--limit-rate 25k` on a 100000-byte download averaged about 33500 B/s, a third over the limit. curl's limiter (8.x `Curl_pgrsLimitWaitTime`) also lets the start burst a little; measure curl 8.22.0 and the port side by side on the same loopback server for 100 KB and 1 MB at 25k and 100k, five runs each, and compare averages. Speed itself is not a goal (no speed parity with C curl), but a rate limit that overshoots by a third is behaviour a user sets the option to control. Use an injected `TimeProvider` in any unit test, never real sleeps.

## Acceptance criteria

- [x] The Notes record curl 8.22.0's and the port's average speeds for the four cases above.
- [x] The port's average is within 10 percent of the limit whenever curl's is, pinned by a unit test of the limiter with a fake clock.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured wall-clock bytes/s on a loopback HttpListener, process start included (curl 8.22.0 vs the port at b9ca725077's successor, before the fix):
- 100 KB @ 25k: curl 23966 (5 runs), port 22230 (first run 5719 was a cold start; the 4 warm runs about 26000 to 28700).
- 100 KB @ 100k: curl 96744, port 114196 (the port wrote the whole body at once, no wait after the last piece).
- 1 MB @ 100k: curl 98846 (3 runs), port 97043.
- 1 MB @ 25k: curl 24873 (1 run), port 24095 (1 run).
Cause: RateLimitedStream waited before each write, never after the last, so the last second's worth of bytes was free (4 pieces of 25000 took 3 s: 33 kB/s). curl waits for its tokens to turn positive after the last drain, so its average sits at the limit. Fix: WriteAsync waits after each piece. Decided by Claude: no ADR, a bug fix inside the stream. Tests: Curl.Core.UnitTests RateLimitedStreamTests (100 KB at 25k averages 25000; 100 KB at 100k takes 1 s), and CurlCommandRunnerRetryTests waits now 1000, 1000, 500. Quality run for Curl.Core.UnitLibrary: 0 failing members.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 8.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 199df755 1eb9a7cc 3d836d5e.
- 2026-11-20: Doing -> Done. Write waits after each piece; average speed now matches the limit
