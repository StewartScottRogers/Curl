---
id: BL-773
title: Keep -v lines and standard-output write failures per transfer under -Z
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-773 — Keep -v lines and standard-output write failures per transfer under -Z

## Goal

Under `-Z`, one transfer's `-v` hold and one transfer's standard-output write failure no longer affect the other transfers running at the same time.

## Context

- ADR-0127 decision 2 and 5; BL-519 Notes, "Left for follow-ups".
- Today `CurlCommandRunner.eventStandardError` is one `HoldableStream` for the run: when a transfer's handler reports it done, its `TransferProgressRecorder` holds the stream until that transfer's meter is written, so other running transfers' `-v` lines wait too (ADR-0127 row 17 has them interleave line by line).
- `StandardOutputFailureDeferringStream` records one write failure for the run; `TransferToStandardOutputAsync` clears it as each transfer starts, so under `-Z` a transfer can clear or see another's failure.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test with `HeldTransferHandler` under `-Z -v` shows a second transfer's `-v` line written while the first transfer's meter is still held.
- [ ] A test under `-Z` with a standard output that fails for one transfer's write shows only that transfer ending with exit 23.
- [ ] Runs without `-Z` are unchanged (existing tests pass), and `Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-09-28: Created.
