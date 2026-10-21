---
id: BL-2892
title: Move create_dir_hierarchy (OutputFileDirectories) into a library the unit-program ports reach, for tool1720
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/OutputFileDirectories.cs, Curl.Console.UnitTests/OutputFileDirectoriesTests.cs, Curl.Core.UnitLibrary/OutputDirectoryHierarchy.cs, Curl.Core.UnitTests/OutputDirectoryHierarchyTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2892 — Move create_dir_hierarchy (OutputFileDirectories) into a library the unit-program ports reach, for tool1720

## Goal

curl's `create_dir_hierarchy` (Curl's `OutputFileDirectories`, now in `Curl.Console`) lives in a library with a seam that reports each directory it would create, so the port of `tests/tunit/tool1720.c` (BL-2845) can check it.

## Context

tool1720.c reads a trace of the directories made: `foo/bar/filename` gives `foo|foo/bar|`, `/foo/bar/filename` gives `/foo|/foo/bar|`, a bare file name gives none, and on Windows drive and backslash forms too (`C:foo\bar\filename` gives `C:foo|C:foo\bar|`). Write it platform-neutral: take the Windows rule set as a parameter (as `runsOnWindows` does today) so both rule sets run on every OS. Source: https://github.com/curl/curl/blob/curl-8_22_0/src/tool_dirhie.c.

## Acceptance criteria

- [ ] The counterpart is in a library `Curl.Conformance.UnitProgram.UnitLibrary` references, with a seam that records the directories, and gives every row of tool1720.c's table for both rule sets on every OS.
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
