---
id: BL-1421
title: Cover CommandLineParser.PeekNext, RefuseUnlistedLetter and AccountHomeDirectory's Windows gap in Curl.Cli.UnitLibrary
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1421 — Cover CommandLineParser.PeekNext, RefuseUnlistedLetter and AccountHomeDirectory's Windows gap in Curl.Cli.UnitLibrary

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` on Windows reports no failing member.

## Context

- Found by BL-1416 on 2026-10-03 (99.98% line, 99.89% branch), in code BL-1416 did not change:
  - `ArgumentReader.PeekNext()` (`CommandLineParser.cs` ~629): the `?? string.Empty` branch for a null argument is never taken.
  - `CommandLineParser.RefuseUnlistedLetter` (`CommandLineParser.cs` ~415): one branch of `CurlOptionAliasTable.IsLetter` is never taken; if every curl letter now has a table row, the branch is dead and the method should say so.
  - `AccountHomeDirectory.cs:15`: the user-profile lambda only runs off Windows, so the Windows measurement never covers it.

## Acceptance criteria

- [x] Each of the three members is covered by a test, or the dead branch is removed.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

- `RefuseUnlistedLetter`: every curl 8.21.0 letter already has a `CommandLineOptionTable` row, so the `IsLetter` true branch was dead. Removed it (and the now-unused `CurlOptionAliasTable.IsLetter` and its `Letters` set), as `RefuseUnlistedNegation` did for names; a letter without a row is unknown. The two letter loops in `CommandLineUnimplementedOptionTests` iterated an empty set; replaced by `EveryCurlLetter_HasARow`, which fails if a letter loses its row.
- `PeekNext`: covered by `Parse_ExpandedFlagFollowedByANullArgument_IsRefusedAsGivenAnEmptyValue` (a null element reads as empty, as `TryTakeNext` does).
- `AccountHomeDirectory`: the lambda became the public `ReadFromUserDatabase()`, tested on every platform; `ForProcess` still calls it only off Windows.
- Measured: Curl.Cli.UnitLibrary 100% line, 100% branch, 0 failing of 1051 members, worst CRAP 10.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl.Cli.UnitLibrary measures 100% line and branch coverage on Windows
