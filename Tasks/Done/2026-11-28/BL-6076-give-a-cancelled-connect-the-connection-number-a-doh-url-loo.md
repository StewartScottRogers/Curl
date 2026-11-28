---
id: BL-6076
title: Give a cancelled connect the connection number a --doh-url look-up reserved for it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitTests/ConnectionCacheSubTransferNumberingTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6076 — Give a cancelled connect the connection number a --doh-url look-up reserved for it

## Goal

A connect that is cancelled (for example by `-m`) after a `--doh-url` look-up numbered it early takes the number it reserved, so `-v` and `--trace-ids` print the same connection number a successful or failed connect would.

## Context

Found by the shift-end review (ADR-0710, review-20261127-022422, code reviewer) of BL-6058 / ADR-1463.

BL-6058 added `ConnectionCache.ReserveForOpening` / `NumberOpened` and used them in
`Curl.Networking.UnitLibrary/PoolingConnector.cs` `OpenAsync` (around lines 605-627) for the connected and
failed results. The `catch` path still calls `OfferNumberToCanceledConnect` (line ~666), which hands
the cancellation `ConnectCancellationKeys.ConnectionNumberTaker = _cache.NumberNextConnection`. When a
DoH look-up inside `openAsync` has already called `ConnectionCache.NumberSubTransferConnection`, the
reservation holds the connection's early number (say `#0`, DoH `#1`), but the cancelled connect
takes a new number (`#2`). Number `#0` goes unused and the `closing connection #N` line prints `#2`
where curl, which numbers a connection before it resolves its host (ADR-1463), prints `#0`.

## Acceptance criteria

- [x] `OfferNumberToCanceledConnect` (or its caller) gives a cancelled non-QUIC connect a taker that returns `_cache.NumberOpened(numbering)`, so a reserved number is used and an unreserved one still takes the next number.
- [x] A unit test in `Curl.Networking.UnitTests/ConnectionCacheSubTransferNumberingTests.cs` has an inner connector call `NumberSubTransferConnection` and then throw `OperationCanceledException`; the exception's `ConnectionNumberTaker` gives `0` and the sub-transfer connection got `1`.
- [x] The existing cancelled-connect numbering tests still pass unchanged.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The taker OfferNumberToCanceledConnect hands over now calls NumberOpened with the open's reservation. The new test failed before the fix and passes after it.
- No new ADR: this applies ADR-1463 (a connection is numbered before its host is resolved) to the cancelled connect.
- 2026-11-27: build clean, fast tests green, Measure-CodeQuality for Curl.Networking.UnitLibrary reports 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 5803d7d8 68c9dd61 21d6d7bb 3d836d5e.
- 2026-11-27: Doing -> Done. A cancelled connect now takes the connection number a --doh-url look-up reserved for it
