---
id: BL-2903
title: Give WindowsOutputFileNameSanitizer sanitize_file_name's SANITIZE_ALLOW_PATH and SANITIZE_ALLOW_RESERVED modes for tool1604
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Globbing/WindowsOutputFileNameSanitizer.cs, Curl.Core.UnitTests/Globbing/WindowsOutputFileNameSanitizerTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2903 — Give WindowsOutputFileNameSanitizer sanitize_file_name's SANITIZE_ALLOW_PATH and SANITIZE_ALLOW_RESERVED modes for tool1604

## Goal

`WindowsOutputFileNameSanitizer` has curl's `sanitize_file_name` flags `SANITIZE_ALLOW_PATH` and `SANITIZE_ALLOW_RESERVED`, so the port of `tests/tunit/tool1604.c` (BL-2845) passes on every OS.

## Context

Today it has one mode, `Sanitize(string)`. tool1604.c's table covers banned characters, drive letters, `\\?\` paths, reserved device names (`com1`, `CoM1`, `con.air`), trailing dots and spaces, and a NULL input (`SANITIZE_ERR_BAD_ARGUMENT`). Source: https://github.com/curl/curl/blob/curl-8_22_0/src/tool_doswin.c and tests/tunit/tool1604.c. Pure string work, so it runs on every OS.

## Acceptance criteria

- [ ] The sanitizer takes the two flags and gives every non-MSDOS row of tool1604.c (output and result).
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
