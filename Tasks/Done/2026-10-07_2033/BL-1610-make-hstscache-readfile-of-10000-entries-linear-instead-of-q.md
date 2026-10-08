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
completed: 2026-10-07
---
# BL-1610 — Make HstsCache.ReadFile of 10000 entries linear instead of quadratic

## Goal

`HstsCache.ReadFile` and `ApplyHeader` find an entry by host without scanning every held entry, so `HstsCacheTests.ApplyHeader_PastMaxEntries_DropsTheFirst` (10000 entries) runs well under the 3-second test budget; no observable behaviour changes.

## Context

- Found by BL-1463: that test printed `SLOW: Curl.Core.Hsts.HstsCacheTests.ApplyHeader_PastMaxEntries_DropsTheFirst took 3099 ms (budget 3000 ms)` under a loaded lane machine; its phases are `PHASE read file: 1616 ms` and `PHASE apply header: 0 ms` on a quiet run.
- Cause: `Curl.Core.UnitLibrary/Hsts/HstsCache.cs` `AddFileEntry` calls `IndexOf`/`IndexOfName`, a linear scan of `entries`, for every line read, so reading `MaxEntries` (10000) lines costs about 50 million host comparisons. `Add` also does `entries.RemoveAt(0)` (O(n)) when full.
- Keep the order of `Entries` (oldest first, as `FormatFile` writes and curl's list keeps), parent-domain matching with `includeSubDomains`, and expiry removal exactly as now; an index from exact host name to entry beside the list is enough for the exact-match lookup.

## Acceptance criteria

- [x] `ApplyHeader_PastMaxEntries_DropsTheFirst` reports `PHASE read file` under 200 ms on `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~HstsCacheTests" --logger "console;verbosity=detailed"`.
- [x] Every existing `HstsCache` and `RedirectFollowerHsts` test passes unchanged.
- [x] `Curl.Core.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10 per method (`Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`).

## Notes

- `HstsCache` keeps a `Dictionary<string, int>` of each host's position beside the list, keyed
  by the new `AsciiCaseInsensitiveComparer` (only A-Z fold, as `AsciiText` and curl's
  `curl_strnequal`), plus `earliestExpiry`, a lower bound on every held expiry. While the clock
  is before it no entry can have expired, so an exact lookup is one dictionary hit and a parent
  lookup tries each dot-suffix of the name, longest first. Once an entry may have expired the
  original in-order scan runs unchanged (removing the expired entries it passes, logging them)
  and the index is rebuilt, so expiry removal stays exactly as it was.
- Dropping the first entry when full counts a `droppedCount` offset rather than rebuilding;
  `max-age=0` removal rebuilds. A host past `MaxHostLength` can be held twice (unchanged
  behaviour); the index keeps its first position and a drop rebuilds while such a repeat is held.
- Measured: `PHASE read file: 20 ms` (was 1616 ms), `apply header: 0 ms`. Hsts tests 151/151
  (two added: a dropped repeated host, and parent/exact lookups past expired entries, which keep
  the scan path at 100% branch coverage). `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`:
  100% line, 100% branch, worst CRAP 10, no failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. HstsCache looks hosts up through a name index; reading 10000 entries takes 20 ms instead of 1.6 s
