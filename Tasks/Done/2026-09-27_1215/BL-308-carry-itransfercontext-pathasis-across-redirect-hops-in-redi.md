---
id: BL-308
title: Carry ITransferContext.PathAsIs across redirect hops in RedirectFollower
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-293]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-308 — Carry ITransferContext.PathAsIs across redirect hops in RedirectFollower

## Goal

A redirect hop built by `RedirectFollower.NextHop` carries the first hop's `PathAsIs`, so `--path-as-is` still applies after `-L` follows a redirect.

## Context

- BL-293 added `ITransferContext.PathAsIs`, but `Curl.Core.UnitLibrary` was held by BL-275 at the time, so `RedirectFollower.NextHop` (`Curl.Core.UnitLibrary/RedirectFollower.cs`) still copies every member except it; a hop therefore reverts to removing dot segments.
- No handler that follows redirects reads `PathAsIs` yet (BL-245 and BL-186 bring it to HTTP), so this is latent today.

## Acceptance criteria

- [x] `RedirectFollower.NextHop` copies `PathAsIs`, and a test in `Curl.Core.UnitTests/RedirectFollowerTests.cs` shows a second hop sees `PathAsIs == true` when the first had it.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member in `Curl.Core.UnitLibrary`.

## Notes

- `NextHop` now sets `PathAsIs = first.PathAsIs`, placed in `TransferContext` declaration order. Test: `FollowAsync_FirstHopPathAsIs_SecondHopKeepsPathAsIs`. Build clean, fast tests green, Curl.Core.UnitLibrary 100% line/branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Redirect hops keep --path-as-is: RedirectFollower.NextHop copies PathAsIs
