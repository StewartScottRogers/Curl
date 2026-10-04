---
id: BL-1427
title: Store TransferResult's and TimeCondition's times as Unix seconds so a time past year 9999 can travel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: FR-011
created: 2026-10-03
completed: 2026-10-03
---
# BL-1427 — Store TransferResult's and TimeCondition's times as Unix seconds so a time past year 9999 can travel

## Goal

`TransferResult` and `TimeCondition` store their time as Unix seconds (`long`), so a time past 9999-12-31T23:59:59Z can travel from a protocol handler to `-R` and `-z`, while their `DateTimeOffset` properties stay as in-range views and no other project changes.

## Context

- ADR-0410, decision 1. `Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs` (positional `DateTimeOffset? SourceLastWriteTimeUtc`) and `TimeCondition.cs` (positional `DateTimeOffset Value`).
- curl keeps these as 64-bit `time_t`; curl 8.21.0 stamps a file from `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT` (measured, BL-1409), which a `DateTimeOffset` cannot hold.
- Additive: every existing handler, `Curl.Console` and `Curl.Cli.UnitLibrary` must compile and pass unchanged.

## Acceptance criteria

- [x] `TransferResult` has `long? SourceLastWriteUnixSeconds` holding the one stored value; setting `SourceLastWriteTimeUtc` stores `ToUnixTimeSeconds()` of it, and reading `SourceLastWriteTimeUtc` gives the time for seconds in `DateTimeOffset`'s range and `null` for seconds outside it (test: `SourceLastWriteUnixSeconds = 1200110860800` (40000-01-01) reads back `null` from `SourceLastWriteTimeUtc`).
- [x] `TimeCondition` has `long ValueUnixSeconds` stored the same way, with `Value` the in-range view; constructing it from a `DateTimeOffset` still works, and a constructor or factory from Unix seconds exists.
- [x] `with { SourceLastWriteTimeUtc = x }` and record equality behave as today for in-range times (tests).
- [x] `dotnet build` is clean and the fast tests pass with no change outside `Curl.Protocol.Abstractions.UnitLibrary` and its `.UnitTests`.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 0 failing members.
- [x] No option changes, so `--ai-help` is unaffected (stated in Notes).

## Notes

- Positional parameters stay as they were (`DateTimeOffset? SourceLastWriteTimeUtc`, `DateTimeOffset Value`), so every constructor call, factory and deconstruction compiles unchanged. Each record redeclares that property as a view whose `init` stores `ToUnixTimeSeconds()` into the new `long` property, the only stored value, so record equality and `with` compare whole seconds.
- `TimeCondition.Value` is non-nullable, so for seconds outside `DateTimeOffset`'s range it clamps to `DateTimeOffset.MaxValue`/`MinValue`: a year-40000 `-z` compares as the latest time a `DateTimeOffset` holds until the HTTP and `file://` tasks of ADR-0410 compare `ValueUnixSeconds`. `TimeCondition.FromUnixSeconds(long, TimeConditionKind)` is the factory.
- The range conversion lives in one internal `UnixSeconds` class shared by both records, tested in `UnixSecondsTests`.
- No command-line option changes, so `--ai-help` is unaffected.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`: 0 failing members. `dotnet build` clean; fast tests all green.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. TransferResult and TimeCondition store times as Unix seconds, so a time past year 9999 can travel
