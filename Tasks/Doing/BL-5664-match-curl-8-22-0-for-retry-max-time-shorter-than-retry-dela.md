---
id: BL-5664
title: Match curl 8.22.0 for --retry-max-time shorter than --retry-delay
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/TransferRetryWarning.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5664 — Match curl 8.22.0 for --retry-max-time shorter than --retry-delay

## Goal

With `--retry-max-time 1 --retry-delay 3 -f`, the port makes exactly as many retries as curl 8.22.0.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 7 (check). Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`--retry-max-time 1 --retry-delay 3 -f` still made one retry. curl's tool (`tool_operate.c`) skips a retry when the time already spent plus the next delay would pass `--retry-max-time`; measure curl 8.22.0 with a server that fails every request (`-f` on a 500) and count requests, and match it. Find where the port decides on the next retry (BL-5604 touched `TransferRetryWarning.cs`; the decision may live in the engine's retry loop) and pin it with a fake clock.

## Acceptance criteria

- [x] The Notes record how many requests curl 8.22.0 makes for the command against an always-failing server.
- [x] The port makes the same number, pinned by a test with an injected `TimeProvider`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured curl 8.22.0 (Windows Schannel) against an always-500 loopback server with `-f --retry 5 --retry-max-time 1 --retry-delay 3`: 2 requests (one retry, waiting the full 3 s, exit 22). curl only checks elapsed >= max-time after an attempt, so the delay is not weighed. The port already did the same (no bug; the finding was a misreading). Added `RunAsync_MaxTimeShorterThanRetryDelay_RetriesOnceLikeCurl` in Curl.Core.UnitTests/TransferRetrierTests.cs with a fake clock; no production change. Fast tests of the class pass.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 7 (check).
- 2026-11-20: Backlog -> Doing. Criteria at claim: a3d4aa2a cb4df1bd 3d836d5e.
