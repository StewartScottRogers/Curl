---
id: BL-1421
title: Store TransferResult's and TimeCondition's times as Unix seconds so a time past year 9999 can travel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1421 — Store TransferResult's and TimeCondition's times as Unix seconds so a time past year 9999 can travel

## Goal

`TransferResult` and `TimeCondition` store their time as Unix seconds (`long`), so a time past 9999-12-31T23:59:59Z can travel from a protocol handler to `-R` and `-z`, while their `DateTimeOffset` properties stay as in-range views and no other project changes.

## Context

- ADR-0410, decision 1. `Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs` (positional `DateTimeOffset? SourceLastWriteTimeUtc`) and `TimeCondition.cs` (positional `DateTimeOffset Value`).
- curl keeps these as 64-bit `time_t`; curl 8.21.0 stamps a file from `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT` (measured, BL-1409), which a `DateTimeOffset` cannot hold.
- Additive: every existing handler, `Curl.Console` and `Curl.Cli.UnitLibrary` must compile and pass unchanged.

## Acceptance criteria

- [ ] `TransferResult` has `long? SourceLastWriteUnixSeconds` holding the one stored value; setting `SourceLastWriteTimeUtc` stores `ToUnixTimeSeconds()` of it, and reading `SourceLastWriteTimeUtc` gives the time for seconds in `DateTimeOffset`'s range and `null` for seconds outside it (test: `SourceLastWriteUnixSeconds = 1200110860800` (40000-01-01) reads back `null` from `SourceLastWriteTimeUtc`).
- [ ] `TimeCondition` has `long ValueUnixSeconds` stored the same way, with `Value` the in-range view; constructing it from a `DateTimeOffset` still works, and a constructor or factory from Unix seconds exists.
- [ ] `with { SourceLastWriteTimeUtc = x }` and record equality behave as today for in-range times (tests).
- [ ] `dotnet build` is clean and the fast tests pass with no change outside `Curl.Protocol.Abstractions.UnitLibrary` and its `.UnitTests`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 0 failing members.
- [ ] No option changes, so `--ai-help` is unaffected (stated in Notes).

## Notes

## Log

- 2026-10-03: Created.
