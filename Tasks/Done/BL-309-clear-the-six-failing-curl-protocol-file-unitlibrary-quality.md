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
completed: 2026-09-26
---
# BL-309 — Clear the six failing Curl.Protocol.File.UnitLibrary quality members

## Goal

`Measure-CodeQuality.ps1` reports no failing member in `Curl.Protocol.File.UnitLibrary`.

## Context

- Found by BL-293 (measured 2026-09-26); none of the six was introduced by it.
- Complexity over 10 as the coverage report measures it: `FileProtocolHandler.DownloadFromAsync` (20, `FileProtocolHandler.cs:211`), `FileUrlPath.RemoveDotSegments` (16, `FileUrlPath.cs:416`), `FileProtocolHandler.MeetsTimeCondition` (12, `FileProtocolHandler.cs:690`). Split each into smaller named methods without changing behaviour.
- 0% line coverage on the compiler-generated members of the positional record `FileUrlPath` (`FileUrlPath.cs:46`): its copy constructor and the `UrlPath` and `OsPath` init accessors, reached only by a `with` expression. Either cover them with a test or stop generating them (for example a sealed class with get-only properties), whichever keeps the public surface honest.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` lists 0 failing members for `Curl.Protocol.File.UnitLibrary`, at 100% line and branch coverage.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, with every existing `Curl.Protocol.File.UnitTests` test unchanged in what it asserts.

## Notes

- Delivered directly rather than through the full `/feature` stages: a behaviour-preserving refactor inside one library, with no new behaviour to plan, conform or document.
- `DownloadFromAsync` (20) now ends after the no-body check; the window, seek and copy moved to `DownloadBodyAsync`, `TrySeekToStart` and `CopyWindowAsync` (the static-lambda caching in the copy call costs complexity on its own, so it sits alone).
- `RemoveDotSegments` (16) delegates each segment to `KeepOrDropSegment` and the trailing test to `IsDotSegment`.
- `MeetsTimeCondition` (12) keeps the unknown-side short circuit; the whole-second comparison moved to `MeetsKnownTimeCondition`.
- `FileUrlPath`: a sealed class was tried first and broke `TryParse_DriveLetterAuthority_MatchesTheEmptyAuthorityForm`, which asserts value equality, so it stays a record. It is now non-positional with a private constructor and get-only properties, so no init accessors are generated and only `TryParse` builds one. The copy constructor a record always has is covered by the new test `With_NoChanges_CopiesAnEqualPath`, since `with` is part of a record's public surface.
- Measured after: 65 members, 0 failing, 100% line and branch, worst CRAP 10. Build clean (0 warnings), 16 fast test assemblies green, `dotnet format --verify-no-changes` clean for both projects.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Protocol.File.UnitLibrary measures 0 failing members at 100% line and branch coverage
