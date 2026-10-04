---
id: BL-1417
title: Cover CommandLineParser.PeekNext, RefuseUnlistedLetter and AccountHomeDirectory's Windows gap in Curl.Cli.UnitLibrary
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1417 — Cover CommandLineParser.PeekNext, RefuseUnlistedLetter and AccountHomeDirectory's Windows gap in Curl.Cli.UnitLibrary

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` on Windows reports no failing member.

## Context

- Found by BL-1416 on 2026-10-03 (99.98% line, 99.89% branch), in code BL-1416 did not change:
  - `ArgumentReader.PeekNext()` (`CommandLineParser.cs` ~629): the `?? string.Empty` branch for a null argument is never taken.
  - `CommandLineParser.RefuseUnlistedLetter` (`CommandLineParser.cs` ~415): one branch of `CurlOptionAliasTable.IsLetter` is never taken; if every curl letter now has a table row, the branch is dead and the method should say so.
  - `AccountHomeDirectory.cs:15`: the user-profile lambda only runs off Windows, so the Windows measurement never covers it.

## Acceptance criteria

- [ ] Each of the three members is covered by a test, or the dead branch is removed.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
