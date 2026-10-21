---
id: BL-2902
title: Move create_dir_hierarchy (OutputFileDirectories) into a library the unit-program ports reach, for tool1720
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/OutputFileDirectories.cs, Curl.Console.UnitTests/OutputFileDirectoriesTests.cs, Curl.Core.UnitLibrary/OutputDirectoryHierarchy.cs, Curl.Core.UnitTests/OutputDirectoryHierarchyTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2902 — Move create_dir_hierarchy (OutputFileDirectories) into a library the unit-program ports reach, for tool1720

## Goal

curl's `create_dir_hierarchy` (Curl's `OutputFileDirectories`, now in `Curl.Console`) lives in a library with a seam that reports each directory it would create, so the port of `tests/tunit/tool1720.c` (BL-2845) can check it.

## Context

tool1720.c reads a trace of the directories made: `foo/bar/filename` gives `foo|foo/bar|`, `/foo/bar/filename` gives `/foo|/foo/bar|`, a bare file name gives none, and on Windows drive and backslash forms too (`C:foo\bar\filename` gives `C:foo|C:foo\bar|`). Write it platform-neutral: take the Windows rule set as a parameter (as `runsOnWindows` does today) so both rule sets run on every OS. Source: https://github.com/curl/curl/blob/curl-8_22_0/src/tool_dirhie.c.

## Acceptance criteria

- [x] The counterpart is in a library `Curl.Conformance.UnitProgram.UnitLibrary` references, with a seam that records the directories, and gives every row of tool1720.c's table for both rule sets on every OS.
- [x] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [x] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Counterpart: public `Curl.Core.OutputDirectoryHierarchy.DirectoriesToCreate(outputFile, runsOnWindows)`, a lazy sequence of the directories curl 8.22.0's `create_dir_hierarchy` passes to `mkdir` (the seam BL-2845 records with `|` after each). Core is public and already referenced by `Curl.Conformance.UnitProgram.UnitLibrary`, so no csproj change. `Curl.Console`'s `OutputFileDirectories` now loops over it and keeps only the create-and-errno step.
- Behaviour change, matching 8.22.0's tool_dirhie.c: under the Windows rule set a first part of exactly two characters whose second is `:` (a bare drive, `C:`) is no longer passed to mkdir; `C:foo` still is. Before, Curl tried `C:` and got EEXIST, so the visible outcome is unchanged.
- `OutputDirectoryHierarchyTests` pins all 11 rows of tool1720.c (shared rows under both rule sets), the Windows rows under the POSIX rule set, and edge cases (`::/x/f`, `/C:/x/f`, doubled separators).
- Measured: `dotnet build Curl.slnx -warnaserror` clean; all fast tests green; Measure-CodeQuality -Library Curl.Core.UnitLibrary,Curl.Console: both 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: 03b31801 3d228e1f d3a9ab15 099d1d27.
- 2026-10-21: Doing -> Done. create_dir_hierarchy's walk is Curl.Core's public OutputDirectoryHierarchy, pinned to every tool1720.c row under both rule sets
