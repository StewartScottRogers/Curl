---
id: BL-1947
title: Bring Curl.Cli.UnitLibrary back to its quality gates: RefuseUnlistedName branch, two complexity-12 methods
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1947 — Bring Curl.Cli.UnitLibrary back to its quality gates: RefuseUnlistedName branch, two complexity-12 methods

## Goal

Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary reports no failing member.

## Context

BL-1943's measurement (2026-10-09) found three members failing that BL-1943 did not change: `CommandLineParser.RefuseUnlistedName` (Curl.Cli.UnitLibrary\CommandLineParser.cs, branch coverage 50%), the `WarnAboutLeadingUnicodeThen` lambda (CommandLineOption.cs:254, cyclomatic complexity 12, from BL-1814) and `CommandLineOptionTable.AddUrlsFromFile` (CommandLineOptionTable.cs:882, complexity 12). Cover the missing branch (or remove it if dead) and extract a private method from each complex member; never raise a threshold.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` lists 0 failing members for Curl.Cli.UnitLibrary.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- `RefuseUnlistedName`'s "known to curl" branch was dead: every `CurlOptionAliasTable` name has a `CommandLineOptionTable` row (the unimplemented-option tests derive an empty set). Removed the branch, the now-unused `RefuseUnlistedNegation` and `CurlOptionAliasTable.TryFindName`; the README no longer describes them. A name in neither table is `is unknown`, as before; a future alias without a row would fail `Parse_EveryAlias_HasARowOrIsNeverRefusedAsUnknown`.
- Complexity: extracted `ReadsValueAsUtf8` from the lambda and `AddUrlFromFileLine` from `AddUrlsFromFile`.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary reports 0 failing members; build -warnaserror clean, fast tests green.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Cli library passes its quality gates
