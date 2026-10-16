---
id: BL-2448
title: Libcurl: curl_getdate and CurlSlist
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Misc/DatesAndLists, Libcurl.UnitTests/Misc/DatesAndLists, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2448 — Libcurl: curl_getdate and CurlSlist

## Goal

Libcurl has `CurlDate.Parse` (`curl_getdate`) over `Curl.Core`'s date parser and `CurlSlist` (`curl_slist_append/_free_all`), which every list-valued option copies on set.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Tests pin upstream's accepted date formats and the -1 result for bad input, and the list copy semantics.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan correction: the date parser is `CurlDateParser` in `Curl.Protocol.Abstractions.UnitLibrary`, not `Curl.Core`; Libcurl reaches it through `Curl.Api.UnitLibrary`'s reference, so no csproj change.
- `CurlDate.Parse` answers -1 (`CurlDate.Invalid`) for refused text; the parser already moves a real -1 to 0 as upstream's `curl_getdate` does. `null` throws.
- `CurlSlist` is an `IReadOnlyList<string>`; `Append(null, s)` starts a list, `FreeAll` empties it. The option setters already copy any `IEnumerable<string>` on set, so the copy semantics hold with no setter change (ADR-0578, decided by Claude under Stewart's delegation).
- Added `Libcurl/CLAUDE.md` to `touches` for the CLAUDE.md criterion; no task in Doing names it.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members; Libcurl.UnitTests 458 passed; 41 fast test projects passed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl has CurlDate.Parse (curl_getdate, -1 for bad text) and CurlSlist (curl_slist_append/_free_all), copied by every list option on set
