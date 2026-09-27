---
id: BL-209
title: Limit the transfer rate for --limit-rate on TimeProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-209 — Limit the transfer rate for --limit-rate on TimeProvider

## Goal

A rate-limiting stream wrapper holds throughput at `--limit-rate` using the injected `TimeProvider`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/manpage.html#--limit-rate (curl 8.21.0).

## Acceptance criteria

- [x] On `FakeTimeProvider`, a 10 KiB transfer at 1 KiB/s takes 10 simulated seconds within one read's tolerance.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Resumed from factory/BL-209-wip (lane 6's partial work), cherry-picked without commit; it was complete, so this run verified it.
- Design: `Curl.Core.RateLimitedStream` wraps the transfer stream. Each read or write moves at most one second's worth of bytes (`bytesPerSecond`), and before each it waits with `Task.Delay(wait, TimeProvider)` until the bytes already moved would have taken that long at the rate. Reads and writes share one count and one clock started at construction. Synchronous `Read`/`Write` throw `NotSupportedException` (async all the way); seeking is unsupported.
- Choice (sensible default, rule 1): a 10 KiB copy at 1 KiB/s makes 10 one-second waits (the last one before the read that returns end of stream), so it ends at 10 simulated seconds; a 10 KiB write ends at 9 because nothing follows the last piece. Both are within one read's tolerance.
- `FakeTimeProvider` in Curl.Core.UnitTests now overrides `GetTimestamp`/`TimestampFrequency` so elapsed time follows its timers.
- Not wired to `--limit-rate` parsing or the transfer path yet; BL-241 (wire retry and rate limiting in Curl.Console) does that.
- Verified 2026-09-27: `dotnet build -warnaserror` clean, fast tests green (Curl.Core.UnitTests 673 passed, 3 skipped), `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lanes cleaned up and cut from 6 to 3 mid-run; partial work saved on branch factory/BL-209-wip: start with git cherry-pick --no-commit factory/BL-209-wip and carry on from it.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. RateLimitedStream holds reads and writes at --limit-rate on the injected TimeProvider; 10 KiB at 1 KiB/s takes 10 simulated seconds
