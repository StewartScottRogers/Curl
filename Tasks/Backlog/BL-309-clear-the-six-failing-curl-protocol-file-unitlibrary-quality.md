---
id: BL-309
title: Clear the six failing Curl.Protocol.File.UnitLibrary quality members
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-309 — Clear the six failing Curl.Protocol.File.UnitLibrary quality members

## Goal

`Measure-CodeQuality.ps1` reports no failing member in `Curl.Protocol.File.UnitLibrary`.

## Context

- Found by BL-293 (measured 2026-09-26); none of the six was introduced by it.
- Complexity over 10 as the coverage report measures it: `FileProtocolHandler.DownloadFromAsync` (20, `FileProtocolHandler.cs:211`), `FileUrlPath.RemoveDotSegments` (16, `FileUrlPath.cs:416`), `FileProtocolHandler.MeetsTimeCondition` (12, `FileProtocolHandler.cs:690`). Split each into smaller named methods without changing behaviour.
- 0% line coverage on the compiler-generated members of the positional record `FileUrlPath` (`FileUrlPath.cs:46`): its copy constructor and the `UrlPath` and `OsPath` init accessors, reached only by a `with` expression. Either cover them with a test or stop generating them (for example a sealed class with get-only properties), whichever keeps the public surface honest.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` lists 0 failing members for `Curl.Protocol.File.UnitLibrary`, at 100% line and branch coverage.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, with every existing `Curl.Protocol.File.UnitTests` test unchanged in what it asserts.

## Notes

## Log

- 2026-09-26: Created.
