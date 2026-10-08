---
id: BL-1610
title: Make HstsCache.ReadFile of 10000 entries linear instead of quadratic
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1610 — Make HstsCache.ReadFile of 10000 entries linear instead of quadratic

## Goal

`HstsCache.ReadFile` and `ApplyHeader` find an entry by host without scanning every held entry, so `HstsCacheTests.ApplyHeader_PastMaxEntries_DropsTheFirst` (10000 entries) runs well under the 3-second test budget; no observable behaviour changes.

## Context

- Found by BL-1463: that test printed `SLOW: Curl.Core.Hsts.HstsCacheTests.ApplyHeader_PastMaxEntries_DropsTheFirst took 3099 ms (budget 3000 ms)` under a loaded lane machine; its phases are `PHASE read file: 1616 ms` and `PHASE apply header: 0 ms` on a quiet run.
- Cause: `Curl.Core.UnitLibrary/Hsts/HstsCache.cs` `AddFileEntry` calls `IndexOf`/`IndexOfName`, a linear scan of `entries`, for every line read, so reading `MaxEntries` (10000) lines costs about 50 million host comparisons. `Add` also does `entries.RemoveAt(0)` (O(n)) when full.
- Keep the order of `Entries` (oldest first, as `FormatFile` writes and curl's list keeps), parent-domain matching with `includeSubDomains`, and expiry removal exactly as now; an index from exact host name to entry beside the list is enough for the exact-match lookup.

## Acceptance criteria

- [ ] `ApplyHeader_PastMaxEntries_DropsTheFirst` reports `PHASE read file` under 200 ms on `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~HstsCacheTests" --logger "console;verbosity=detailed"`.
- [ ] Every existing `HstsCache` and `RedirectFollowerHsts` test passes unchanged.
- [ ] `Curl.Core.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method (`Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`).

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
