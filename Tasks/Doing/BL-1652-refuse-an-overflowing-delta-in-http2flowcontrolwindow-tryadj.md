---
id: BL-1652
title: Refuse an overflowing delta in Http2FlowControlWindow.TryAdjust
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1652 — Refuse an overflowing delta in Http2FlowControlWindow.TryAdjust

## Goal

`Http2FlowControlWindow.TryAdjust` returns `false` and leaves the window unchanged for every delta that would take it past `MaximumSize`, including deltas so large that `Size + delta` overflows a `long`.

## Context

- Found by BL-1499's adversarial review of `Curl.Http2.UnitLibrary`'s public surface. `TryAdjust` checks `Size + delta > MaximumSize` in unchecked arithmetic: with `Size` 1 and `delta` `long.MaxValue` the sum wraps to `long.MinValue`, the check passes, the method returns `true` and `Size` becomes `long.MinValue`. Its doc comment promises `false`, window unchanged, whenever the window would pass the maximum.
- Not reachable from the wire today: `Http2Connection` only passes WINDOW_UPDATE increments (at most 2^31 - 1) and SETTINGS_INITIAL_WINDOW_SIZE differences. It is a public member whose contract is broken, so Normal, not High.
- Fix by comparing without overflow, e.g. `delta > MaximumSize - Size` (`Size` is never above `MaximumSize`, so the right side cannot overflow), and decide the matching rule for a very negative delta that would wrap below `long.MinValue`.
- The test lands with the fix, in `Curl.Http2.UnitTests` (e.g. `Http2FlowControlWindowTests` or `Http2FrameAdversarialTests`).

## Acceptance criteria

- [ ] A test `TryAdjust_DeltaOverflowingLong_RefusesAndKeepsTheSize` in `Curl.Http2.UnitTests` creates `new Http2FlowControlWindow(1)`, calls `TryAdjust(long.MaxValue)`, and asserts `false` and `Size` 1; it fails before the fix and passes after.
- [ ] `dotnet build Curl.Http2.UnitTests -warnaserror` is clean and `dotnet test Curl.Http2.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] `Curl.Http2.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
